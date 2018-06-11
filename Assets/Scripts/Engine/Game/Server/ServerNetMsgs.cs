// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;

namespace Server {

	public partial class ServerWorld {

		void OnNetMsg(NetMsgs.ClientConnect msg, ActorReplicationChannel channel) {
			if (msg.gameVersion != BuildInfo.ID) {
				DisconnectClient(channel.connection, null, EDisconnectReason.WrongVersion, BuildInfo.ID);
				return;
			}
			if (channel.didHandshake) {
				DisconnectClient(channel.connection, null, EDisconnectReason.Error, null);
				return;
			}

			channel.uuid = msg.uuid;
#if BACKEND_SERVER
			channel.challenge = msg.challenge;
#endif
			channel.didHandshake = true;
			channel.isTraveling = true;
			channel.pendingConnect = true;
			channel.levelStarted = false;
			channel.clientLevel = null;
		}

		void OnNetMsg(NetMsgs.ClientFinishedTravel msg, ActorReplicationChannel channel) {
			channel.clientLevel = msg.levelName;
			channel.ResetTimeoutForTravel();

			if (isTraveling) {
				if (channel.clientLevel != travelLevel) {
					// travel
					channel.connection.SendReliable(NetMsgs.ClientTravel.New(travelLevel, null));
				}
			} else if (channel.clientLevel != currentLevel) {
				channel.connection.SendReliable(NetMsgs.ClientTravel.New(currentLevel, null));
			}
		}

		void OnNetMsg(NetMsgs.ClientLevelStarted msg, ActorReplicationChannel channel) {
			channel.ResetTimeoutForTravel();

			if (isTraveling) {
				if (channel.clientLevel == travelLevel) {
					channel.levelStarted = true;
				}
			} else if (channel.clientLevel == currentLevel) {
				channel.levelStarted = true;
			}
		}
	}

}
