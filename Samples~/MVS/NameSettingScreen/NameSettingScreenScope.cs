using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Extreal.Integration.Chat.OME.MVS.NameSettingScreen
{
    public class NameSettingScreenScope : LifetimeScope
    {
        [SerializeField] private NameSettingScreenView nameSettingScreenView;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(nameSettingScreenView);

            builder.RegisterEntryPoint<NameSettingScreenPresenter>();
        }
    }
}
