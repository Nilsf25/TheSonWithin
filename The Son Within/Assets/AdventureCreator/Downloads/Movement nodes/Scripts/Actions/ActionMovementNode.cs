using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using AC.MovementNodes;

namespace AC
{

	[System.Serializable]
	public class ActionMovementNode : Action
	{

		[SerializeField] private MovementNodeMethod method = MovementNodeMethod.MoveToSpecificNode;
		private enum MovementNodeMethod { MoveToSpecificNode, MoveForward, MoveBackward, TurnLeft, TurnRight, DisableDirection, EnableDirection };

		[SerializeField] private MovementNodeMoveMethod moveMethod = MovementNodeMoveMethod.IgnoreOtherNodes;

		public Char charToMove;
		public int charToMoveConstantID;
		public int charToMoveParameterID = -1;

		public bool isPlayer;

		public bool doPathfinding = true;
		public bool isInstant;

		public MovementNode movementNode;
		public int movementNodeConstantID;
		public int movementNodeParameterID = -1;

		public bool setDirection = false;
		public int directionID;

		private Char runtimeChar;

		
		public ActionMovementNode ()
		{
			this.isDisplayed = true;
			category = ActionCategory.Character;
			title = "Movement node";
			description = "Controls a Movement Node";
		}


		public override void AssignValues(System.Collections.Generic.List<ActionParameter> parameters)
		{
			runtimeChar = (isPlayer) ? KickStarter.player : AssignFile <Char> (parameters, charToMoveParameterID, charToMoveConstantID, charToMove);
			movementNode = AssignFile <MovementNode> (parameters, movementNodeParameterID, movementNodeConstantID, movementNode);
		}
		
		
		override public float Run ()
		{
			if (!isRunning)
			{
				Perform (isInstant);
				
				if (method == MovementNodeMethod.MoveToSpecificNode ||
					method == MovementNodeMethod.MoveForward ||
					method == MovementNodeMethod.MoveBackward ||
					method == MovementNodeMethod.TurnLeft ||
					method == MovementNodeMethod.TurnRight)
				{
					if (runtimeChar != null && !isInstant && willWait)
					{
						isRunning = true;
						return defaultPauseTime;
					}
				}

				return 0f;
			}
			else
			{
				if (runtimeChar.IsMovingAlongPath () || runtimeChar.IsTurning ())
				{
					return defaultPauseTime;
				}

				isRunning = false;
				return 0f;
			}
		}


		override public void Skip ()
		{
			Perform (true);
		}


		private void Perform (bool _isInstant)
		{
			if (method == MovementNodeMethod.MoveToSpecificNode ||
				method == MovementNodeMethod.EnableDirection ||
				method == MovementNodeMethod.DisableDirection)
			{
				if (movementNode == null)
				{
					ACDebug.LogWarning ("Can't control Movement Node - no node set!");
					return;
				}
			}

			if (method == MovementNodeMethod.MoveToSpecificNode ||
				method == MovementNodeMethod.MoveForward ||
				method == MovementNodeMethod.MoveBackward ||
				method == MovementNodeMethod.TurnLeft ||
				method == MovementNodeMethod.TurnRight)
			{
				if (runtimeChar == null)
				{
					ACDebug.LogWarning ("Can't control Movement Node - no character set!");
					return;
				}
			}

			switch (method)
			{
				case MovementNodeMethod.MoveToSpecificNode:
					if (_isInstant)
					{
						if (setDirection)
						{
							movementNode.SnapTo (runtimeChar, directionID);
						}
						else
						{
							movementNode.SnapTo (runtimeChar);
						}
					}
					else
					{
						if (setDirection)
						{
							movementNode.MoveTo (runtimeChar, moveMethod, doPathfinding, directionID);
						}
						else
						{
							movementNode.MoveTo (runtimeChar, moveMethod, doPathfinding);
						}
					}
					break;

				case MovementNodeMethod.MoveForward:
					MovementNode.MoveForward (runtimeChar, doPathfinding, _isInstant);
					break;

				case MovementNodeMethod.MoveBackward:
					MovementNode.MoveBackward (runtimeChar, doPathfinding, _isInstant);
					break;

				case MovementNodeMethod.TurnLeft:
					MovementNode.TurnLeft (runtimeChar, _isInstant);
					break;

				case MovementNodeMethod.TurnRight:
					MovementNode.TurnRight (runtimeChar, _isInstant);
					break;

				case MovementNodeMethod.EnableDirection:
					movementNode.EnableDirection (directionID);
					break;

				case MovementNodeMethod.DisableDirection:
					movementNode.DisableDirection (directionID);
					break;
			}
		}

		
		#if UNITY_EDITOR

		override public void ShowGUI (List<ActionParameter> parameters)
		{
			method = (MovementNodeMethod) EditorGUILayout.EnumPopup ("Method:", method);

			if (method == MovementNodeMethod.MoveToSpecificNode ||
				method == MovementNodeMethod.MoveForward ||
				method == MovementNodeMethod.MoveBackward ||
				method == MovementNodeMethod.TurnLeft ||
				method == MovementNodeMethod.TurnRight)
			{
				isPlayer = EditorGUILayout.Toggle ("Affect Player?", isPlayer);
				if (!isPlayer)
				{
					charToMoveParameterID = Action.ChooseParameterGUI ("Character to move:", parameters, charToMoveParameterID, ParameterType.GameObject);
					if (charToMoveParameterID >= 0)
					{
						charToMoveConstantID = 0;
						charToMove = null;
					}
					else
					{
						charToMove = (Char) EditorGUILayout.ObjectField ("Character to move:", charToMove, typeof (Char), true);

						charToMoveConstantID = FieldToID <Char> (charToMove, charToMoveConstantID);
						charToMove = IDToField <Char> (charToMove, charToMoveConstantID, false);
					}
				}
			}

			if (method == MovementNodeMethod.MoveToSpecificNode ||
				method == MovementNodeMethod.EnableDirection ||
				method == MovementNodeMethod.DisableDirection)
			{
				bool showDirectionGUI = false;
				string label = (method == MovementNodeMethod.MoveToSpecificNode) ? "Node to move to:" : "Node to affect:";

				movementNodeParameterID = Action.ChooseParameterGUI (label, parameters, movementNodeParameterID, ParameterType.GameObject);
				if (movementNodeParameterID >= 0)
				{
					movementNodeConstantID = 0;
					movementNode = null;
				}
				else
				{
					movementNode = (MovementNode) EditorGUILayout.ObjectField (label, movementNode, typeof (MovementNode), true);

					movementNodeConstantID = FieldToID <MovementNode> (movementNode, movementNodeConstantID);
					movementNode = IDToField <MovementNode> (movementNode, movementNodeConstantID, false);

					showDirectionGUI = true;
				}

				if (movementNode != null && 
					(method == MovementNodeMethod.MoveToSpecificNode ||
					 method == MovementNodeMethod.EnableDirection ||
					 method == MovementNodeMethod.DisableDirection))
				{
					if (method == MovementNodeMethod.MoveToSpecificNode)
					{
						setDirection = EditorGUILayout.Toggle ("Set final direction?", setDirection);
					}

					if (setDirection || method != MovementNodeMethod.MoveToSpecificNode)
					{
						if (showDirectionGUI)
						{
							directionID = movementNode.ShowDirectionGUI (directionID, "Direction:");
						}
						else
						{
							directionID = EditorGUILayout.IntField ("Direction ID:", directionID);
						}
					}
				}
			}

			if (method == MovementNodeMethod.MoveToSpecificNode ||
				method == MovementNodeMethod.MoveForward ||
				method == MovementNodeMethod.MoveBackward ||
				method == MovementNodeMethod.TurnLeft ||
				method == MovementNodeMethod.TurnRight)
			{
				isInstant = EditorGUILayout.Toggle ("Is instant?", isInstant);
				if (!isInstant)
				{
					if (method == MovementNodeMethod.MoveToSpecificNode)
					{
						moveMethod = (MovementNodeMoveMethod) EditorGUILayout.EnumPopup ("Move method:", moveMethod);
					}

					if (method == MovementNodeMethod.MoveToSpecificNode ||
						method == MovementNodeMethod.MoveForward ||
						method == MovementNodeMethod.MoveBackward)
					{
						doPathfinding = EditorGUILayout.Toggle ("Pathfind?", doPathfinding);
					}

					willWait = EditorGUILayout.Toggle ("Wait until finish?", willWait);
				}
			}

			AfterRunningOption ();
		}


		override public void AssignConstantIDs (bool saveScriptsToo, bool fromAssetFile)
		{
			if (saveScriptsToo)
			{
				AddSaveScript <RememberMovementNode> (movementNode);
			}
			AssignConstantID <Char> (charToMove, charToMoveConstantID, charToMoveParameterID);
			AssignConstantID <MovementNode> (movementNode, movementNodeConstantID, movementNodeParameterID);
		}
		

		public override string SetLabel ()
		{
			return method.ToString ();
		}

		#endif
		
	}

	public enum MovementNodeMoveMethod { IgnoreOtherNodes, MoveAlongNodePath, MoveOneStepTowards };

}