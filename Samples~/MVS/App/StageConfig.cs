using Extreal.Core.StageNavigation;
using UnityEngine;

namespace Extreal.Integration.Chat.OME.MVS.App
{
    [CreateAssetMenu(
        menuName = "Chat.OME.MVS/" + nameof(StageConfig),
        fileName = nameof(StageConfig))]
    public class StageConfig : StageConfigBase<StageName, SceneName>
    {
    }
}
