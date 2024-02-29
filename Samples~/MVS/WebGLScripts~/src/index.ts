import { OmeAdapter } from "@extreal-dev/extreal.integration.sfu.ome";
import { VoiceChatAdapter } from "@extreal-dev/extreal.integration.chat.ome";

const omeAdapter = new OmeAdapter();
omeAdapter.adapt();

const voiceChatAdapter = new VoiceChatAdapter();
voiceChatAdapter.adapt(omeAdapter.getOmeClient);
