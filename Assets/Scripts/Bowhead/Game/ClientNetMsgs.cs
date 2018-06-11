// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;
using NetMsgs;

namespace Bowhead.Client {

	public partial class ClientWorld {
		protected override void OnNetMsg(NetMsgs.ClientTravel msg, ActorReplicationChannel channel) {
			base.OnNetMsg(msg, channel);

			if (GameManager.instance.serverWorld != null) {
				if (!GameManager.instance.serverWorld.isTraveling) {
					// server does the traveling.
					serverConnection.SendReliable(NetMsgs.ClientFinishedTravel.New(msg.levelName));
				}
				return;
			}

			BeginTravel(msg.levelName, msg.travelActorNetIDs);
			GameManager.instance.SetPendingLevel(msg.levelName, null);
		}
	}

}