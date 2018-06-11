// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;

namespace Client {

	public partial class ClientWorld {
		void OnNetMsg(NetMsgs.ReplicatedObjectData msg, ActorReplicationChannel channel) {
			if (channel == _serverConnection) {
				_serverConnection.HandleReplicatedObjectData(msg);
			}
		}

		void OnNetMsg(NetMsgs.Welcome msg, ActorReplicationChannel channel) {
			if (channel == _serverConnection) {
				Debug.Log("Welcomed by server: " + msg.serverName);
				Debug.Log(msg.message);
				Debug.Log("ConnectionID = " + msg.connectionID);
				_serverConnection.connection.SetID(msg.connectionID);
				_connectionState = EConnectionState.Joining;
				SendClientConnect();
			}
		}

		void OnNetMsg(NetMsgs.DestroyActor msg, ActorReplicationChannel channel) {
			var actor = (Actor)GetObjectByNetID(msg.netID);
			if (actor != null) {
				DestroyAndRemoveActor(actor);
            }
		}

		protected abstract void SendClientConnect();

		protected virtual void OnNetMsg(NetMsgs.ServerFinishedTravel msg, ActorReplicationChannel channel) {
			_serverTravel = false;
		}

		protected virtual void OnNetMsg(NetMsgs.ClientTravel msg, ActorReplicationChannel channel) {
			_serverTravel = true;
		}
	}

}