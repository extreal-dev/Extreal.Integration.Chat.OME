using VContainer;
using VContainer.Unity;

namespace Extreal.Integration.Chat.OME.MVS.OmeControl
{
    public class OmeControlScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
            => builder.RegisterEntryPoint<OmeControlPresenter>();
    }
}
