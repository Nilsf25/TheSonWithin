using UnityEngine;
using System.Collections;

namespace AC.MovementNodes
{

	[RequireComponent (typeof (MovementNode))]
	public class RememberMovementNode : Remember
	{

		private MovementNode movementNode;
		
		
		public override string SaveData ()
		{
			MovementNodeData movementNodeData = new MovementNodeData();
			movementNodeData.objectID = constantID;
			movementNodeData.savePrevented = savePrevented;

			movementNodeData = MovementNode.SaveData (movementNodeData);

			return Serializer.SaveScriptData <MovementNodeData> (movementNodeData);
		}


		public override void LoadData (string stringData)
		{
			MovementNodeData data = Serializer.LoadScriptData <MovementNodeData> (stringData);
			if (data == null) return;
			SavePrevented = data.savePrevented; if (savePrevented) return;

			MovementNode.LoadData (data);
		}


		private MovementNode MovementNode
		{
			get
			{
				if (movementNode == null)
				{
					movementNode = GetComponent <MovementNode>();
				}
				return movementNode;
			}
		}

	}


	/**
	 * A data container used by the RememberMovementNode script.
	 */
	[System.Serializable]
	public class MovementNodeData : RememberData
	{

		/** Information about which directions are enabled */
		public string directionStates;
		/** The Constant ID number of the currently-occupied character */
		public int occupiedCharacterID;
		/** True if the MovementNode is being used by the Player */
		public bool occupiedByPlayer;

		/**
		 * The default Constructor.
		 */
		public MovementNodeData () { }

	}

}