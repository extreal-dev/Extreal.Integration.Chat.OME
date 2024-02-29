using Extreal.Core.Logging;
using Extreal.Core.StageNavigation;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Extreal.Integration.Chat.OME.MVS.App
{
    public class AppScope : LifetimeScope
    {
        [SerializeField] private StageConfig stageConfig;

        private static void InitializeApp()
        {
            const LogLevel logLevel = LogLevel.Debug;
            LoggingManager.Initialize(logLevel: logLevel);

#if UNITY_WEBGL && !UNITY_EDITOR
            var config = new Extreal.Integration.Web.Common.WebGLHelperConfig { IsDebug = true };
            Extreal.Integration.Web.Common.WebGLHelper.Initialize(config);
#endif
        }

        protected override void Awake()
        {
            InitializeApp();
            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<AppState>(Lifetime.Singleton);

            builder.RegisterComponent(stageConfig).AsImplementedInterfaces();
            builder.Register<StageNavigator<StageName, SceneName>>(Lifetime.Singleton);

            builder.RegisterEntryPoint<AppPresenter>();
        }
    }
}
