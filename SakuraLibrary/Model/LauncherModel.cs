﻿using Grpc.Core.Utils;
using Grpc.Net.Client;
using SakuraLibrary.Helper;
using SakuraLibrary.Proto;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using static SakuraLibrary.Proto.NatfrpService;
using UserStatus = SakuraLibrary.Proto.User.Types.Status;

namespace SakuraLibrary.Model
{
    public abstract class LauncherModel : ModelBase, IAsyncManager
    {
        static LauncherModel()
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2Support", true);
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        }

        public static Empty RpcEmpty = new Empty();

        public readonly NatfrpServiceClient RPC = new NatfrpServiceClient(GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = new StandardSocketsHttpHandler()
            {
                ConnectCallback = async (context, cancellationToken) =>
                {
                    var pipe = new NamedPipeClientStream(".", "SakuraFrpService", PipeDirection.InOut, PipeOptions.Asynchronous);
                    try
                    {
                        await pipe.ConnectAsync(cancellationToken);
                    }
                    catch
                    {
                        pipe.Dispose();
                    }
                    return pipe;
                }
            }
        }));
        public readonly DaemonHost Daemon;
        public readonly AsyncManager AsyncManager;

        public LauncherModel(bool forceDaemon = false)
        {
            Daemon = new DaemonHost(this, forceDaemon);
            AsyncManager = new AsyncManager(Run);

            Daemon.Start();
            Start();
        }

        public abstract void Log(Log l, bool init = false);
        public abstract void ClearLog();

        public abstract void Save();

        protected void launcherError(string message)
        {
            //Log(new Log()
            //{
            //    Category = 3,
            //    Source = "Launcher",
            //    Data = message,
            //    Time = Utils.GetSakuraTime()
            //});
        }

        #region IPC Handling

        protected void Run()
        {
            //var userCb = (User u) => UserInfo = u;
            do
            {
                try
                {
                    Task.WaitAll(new Task[]
                    {
                        RPC.StreamUser(RpcEmpty).ResponseStream.ForEachAsync((User u) => UserInfo = u),
                        RPC.StreamLog(RpcEmpty).ResponseStream.ForEachAsync(l => Dispatcher.Invoke(() => Log(l))),
                        RPC.StreamNodes(RpcEmpty).ResponseStream.ForEachAsync(n => Dispatcher.Invoke(() =>
                        {
                            Nodes.Clear();
                            foreach (var node in n.Nodes.Values)
                            {
                                Nodes.Add(new NodeModel(node));
                            }
                            Connected = true;
                        })),
                    });
                }
                catch (Exception ex)
                {
                    Connected = false;
                    Console.WriteLine(ex);
                }
            }
            while (!AsyncManager.StopEvent.WaitOne(500));
        }

        //protected void Pipe_ServerPush(ServiceConnection connection, PushMessageBase msg)
        //{
        //    try
        //    {
        //        switch (msg.Type)
        //        {
        //        case PushMessageID.UpdateTunnel:
        //            Dispatcher.Invoke(() =>
        //            {
        //                foreach (var t in Tunnels)
        //                {
        //                    if (t.Id == msg.DataTunnel.Id)
        //                    {
        //                        t.Proto = msg.DataTunnel;
        //                        t.SetNodeName(Nodes.ToDictionary(k => k.Id, v => v.Name));
        //                        break;
        //                    }
        //                }
        //            });
        //            break;
        //        case PushMessageID.UpdateTunnels:
        //            LoadTunnels(msg.DataTunnels);
        //            break;
        //        case PushMessageID.UpdateNodes:
        //            Dispatcher.Invoke(() =>
        //            {
        //                Nodes.Clear();
        //                var map = new Dictionary<int, string>();
        //                foreach (var n in msg.DataNodes.Nodes)
        //                {
        //                    Nodes.Add(new NodeModel(n));
        //                    map.Add(n.Id, n.Name);
        //                }
        //                foreach (var t in Tunnels)
        //                {
        //                    t.SetNodeName(map);
        //                }
        //            });
        //            break;
        //        case PushMessageID.AppendLog:
        //            Dispatcher.Invoke(() =>
        //            {
        //                foreach (var l in msg.DataLog.Data)
        //                {
        //                    Log(l);
        //                }
        //            });
        //            break;
        //        }
        //    }
        //    catch { }
        //}

        #endregion

        #region Main Window

        public bool Connected { get => _connected; set => Set(out _connected, value); }
        private bool _connected = false;

        public User UserInfo
        {
            get => _userInfo;
            set
            {
                if (value == null)
                {
                    value = new User();
                }
                SafeSet(out _userInfo, value);
            }
        }
        private User _userInfo = new User();

        public ObservableCollection<NodeModel> Nodes { get; set; } = new ObservableCollection<NodeModel>();

        #endregion

        #region Tunnel

        public ObservableCollection<TunnelModel> Tunnels { get; set; } = new ObservableCollection<TunnelModel>();

        public Task<Exception> RequestReloadTunnelsAsync() => RPC.ReloadTunnelsAsync(RpcEmpty).WaitException();

        public Task<Exception> RequestDeleteTunnelAsync(int id) => RPC.UpdateTunnelAsync(new TunnelUpdate()
        {
            Action = TunnelUpdate.Types.Action.Delete,
            Tunnel = new Tunnel() { Id = id }
        }).WaitException();

        #endregion

        #region Settings - User Status

        [SourceBinding(nameof(UserInfo))]
        public string UserToken { get => UserInfo.Status != UserStatus.NoLogin ? "****************" : _userToken; set => SafeSet(out _userToken, value); }
        private string _userToken = "";

        [SourceBinding(nameof(UserInfo))]
        public bool LoggedIn => UserInfo.Status == UserStatus.LoggedIn;

        [SourceBinding(nameof(LoggingIn), nameof(LoggedIn))]
        public bool TokenEditable => !LoggingIn && !LoggedIn;

        [SourceBinding(nameof(UserInfo))]
        public bool LoggingIn { get => _loggingIn || UserInfo.Status == UserStatus.Pending; set => SafeSet(out _loggingIn, value); }
        private bool _loggingIn;

        public async Task LoginOrLogout()
        {
            LoggingIn = true;
            try
            {
                if (LoggedIn)
                {
                    await RPC.LogoutAsync(RpcEmpty).ConfigureAwait(false);
                }
                else
                {
                    await RPC.LoginAsync(new LoginRequest() { Token = UserToken }).ConfigureAwait(false);
                }
            }
            finally
            {
                LoggingIn = false;
            }
        }

        #endregion

        #region Settings - Launcher

        /// <summary>
        /// 0 = Show all
        /// 1 = Suppress all
        /// 2 = Suppress INFO
        /// </summary>
        public int NotificationMode { get => _notificationMode; set => Set(out _notificationMode, value); }
        private int _notificationMode;

        public bool LogTextWrapping { get => _logTextWrapping; set => Set(out _logTextWrapping, value); }
        private bool _logTextWrapping;

        #endregion

        #region Settings - Service

        public ServiceConfig Config
        {
            get => _config;
            set => SafeSet(out _config, value);
        }
        private ServiceConfig _config;

        [SourceBinding(nameof(Config))]
        public bool BypassProxy
        {
            get => Config != null && Config.BypassProxy;
            set
            {
                if (Config != null)
                {
                    Config.BypassProxy = value;
                    PushServiceConfig();
                }
                RaisePropertyChanged();
            }
        }

        public void PushServiceConfig()
        {
            //var result = RPC.Request(new RequestBase()
            //{
            //    Type = MessageID.ControlConfigSet,
            //    DataConfig = Config
            //});
            //if (!result.Success)
            //{
            //    launcherError("无法更新守护进程配置: " + result.Message);
            //}
        }

        #endregion

        #region Settings - Auto Update

        [SourceBinding(nameof(Config))]
        public bool RemoteManagement
        {
            get => Config != null && Config.RemoteManagement;
            set
            {
                if (Config != null)
                {
                    if (!value || !string.IsNullOrEmpty(Config.RemoteManagementKey))
                    {
                        Config.RemoteManagement = value;
                    }
                    PushServiceConfig();
                }
                RaisePropertyChanged();
            }
        }

        [SourceBinding(nameof(Config), nameof(LoggedIn))]
        public bool CanEnableRemoteManagement => LoggedIn && Config != null && !string.IsNullOrEmpty(Config.RemoteManagementKey);

        [SourceBinding(nameof(Config))]
        public bool EnableTLS
        {
            get => Config != null && Config.FrpcForceTls;
            set
            {
                if (Config != null)
                {
                    Config.FrpcForceTls = value;
                    PushServiceConfig();
                }
                RaisePropertyChanged();
            }
        }

        public UpdateStatus Update { get => _update; set => SafeSet(out _update, value); }
        private UpdateStatus _update;

        [SourceBinding(nameof(Update))]
        public bool HaveUpdate => Update != null && Update.UpdateManagerRunning && Update.UpdateAvailable;

        [SourceBinding(nameof(Update))]
        public string UpdateText
        {
            get
            {
                if (Update == null || !Update.UpdateAvailable)
                {
                    return "";
                }
                if (Update.UpdateReadyDir != "")
                {
                    return "更新准备完成, 点此进行更新";
                }
                return "下载更新中... " + Math.Round(Update.DownloadCurrent / 1048576f, 2) + " MiB/" + Math.Round(Update.DownloadTotal / 1048576f, 2) + " MiB";
            }
        }

        [SourceBinding(nameof(Config), nameof(Update))]
        public bool CheckUpdate
        {
            get => Config != null && Config.UpdateInterval != -1 && Update != null && Update.UpdateManagerRunning;
            set
            {
                if (Config != null)
                {
                    Config.UpdateInterval = value ? 86400 : -1;
                    PushServiceConfig();
                }
                if (!value)
                {
                    Update = null;
                }
                RaisePropertyChanged();
            }
        }

        public void RequestUpdateCheck()
        {

        }

        public void ConfirmUpdate(bool legacy, Action<bool, string> callback, Func<string, bool> confirm, Func<string, bool> warn)
        {
            if (Update.UpdateReadyDir == "")
            {
                return;
            }
            if (!confirm(Update.Note))
            {
                return;
            }
            if (NTAPI.GetSystemMetrics(SystemMetric.SM_REMOTESESSION) != 0 && !warn("检测到当前正在使用远程桌面连接，若您正在通过 SakuraFrp 连接计算机，请勿进行更新\n进行更新时启动器和所有 frpc 将彻底退出并且需要手动确认操作，这会造成远程桌面断开并且无法恢复\n是否继续?"))
            {
                return;
            }
            Daemon.Stop();
            try
            {
                Process.Start(new ProcessStartInfo(Consts.ServiceExecutable, "--update \"" + Update.UpdateReadyDir.TrimEnd('\\') + "\" " + (legacy ? "legacy" : "wpf"))
                {
                    Verb = "runas"
                });
            }
            finally
            {
                Environment.Exit(0);
            }
        }

        #endregion

        #region Settings - Working Mode

        public bool IsDaemon => Daemon.Daemon;

        public string WorkingMode => Daemon.Daemon ? "守护进程" : "系统服务";

        public bool SwitchingMode { get => _switchingMode; set => SafeSet(out _switchingMode, value); }
        private bool _switchingMode;

        public void SwitchWorkingMode(Action<bool, string> callback, Func<string, bool> confirm)
        {
            if (SwitchingMode)
            {
                return;
            }
            if (LoggingIn || LoggedIn)
            {
                callback(false, "请先登出当前账户");
                return;
            }
            if (!confirm("确定要切换运行模式吗?\n如果您不知道该操作的作用, 请不要切换运行模式\n如果您不知道该操作的作用, 请不要切换运行模式\n如果您不知道该操作的作用, 请不要切换运行模式\n\n注意事项:\n1. 切换运行模式后不要移动启动器到其他目录, 否则会造成严重错误\n2. 如需移动或卸载启动器, 请先切到 \"守护进程\" 模式来避免文件残留\n3. 切换过程可能需要十余秒, 请耐心等待, 不要做其他操作\n4. 切换操作即为 安装/卸载 系统服务, 需要管理员权限\n5. 切换完成后需要重启启动器"))
            {
                return;
            }
            SwitchingMode = true;
            ThreadPool.QueueUserWorkItem(s =>
            {
                try
                {
                    Daemon.Stop();
                    if (!Daemon.InstallService(!Daemon.Daemon))
                    {
                        callback(false, "运行模式切换失败, 请检查您是否有足够的权限 安装/卸载 服务.\n由于发生严重错误, 启动器即将退出.");
                    }
                    else
                    {
                        callback(true, "运行模式已切换, 启动器即将退出");
                    }
                    Environment.Exit(0);
                }
                finally
                {
                    SwitchingMode = false;
                }
            });
        }

        #endregion

        #region IAsyncManager

        public bool Running => AsyncManager.Running;

        public void Start() => AsyncManager.Start(true);

        public void Stop(bool kill = false) => AsyncManager.Stop(kill);

        #endregion
    }
}
