/*
 *
 *	Adventure Creator
 *	by Chris Burton, 2013-2019
 *	
 *	"MovementNode.cs"
 * 
 *	This script represents a node that a single character can occupy at a time, when relying on grid-based movement.
 * 
 *	A portion of this code is adapted from Hasan Bayat's work at https://github.com/EmpireWorld/unity-dijkstras-pathfinding
 * 
 */

using UnityEngine;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AC.MovementNodes
{

	/**
	 * This script represents a node that a single character can occupy at a time, when relying on grid-based movement.
	 * A portion of this code is adapted from Hasan Bayat's work at https://github.com/EmpireWorld/unity-dijkstras-pathfinding
	 */
	[AddComponentMenu("Adventure Creator/Navigation/Movement node")]
	#if !(UNITY_4_6 || UNITY_4_7 || UNITY_5_0)
	[HelpURL("http://www.adventurecreator.org/scripting-guide/class_a_c_1_1_movement_node.html")]
	#endif
	[RequireComponent (typeof (Paths))]
	public class MovementNode : MonoBehaviour
	{

		#region Variables

		[SerializeField] private string label;

		[SerializeField] private MovementNodeAutoHotspot autoHotspot = MovementNodeAutoHotspot.Always;
		private enum MovementNodeAutoHotspot { Never, OnlyImmediate, Always };

		[SerializeField] private List<NodeDirection> nodeDirections = new List<NodeDirection>();
		[SerializeField] private PlayerStart linkedPlayerStart;
		[SerializeField] private bool canCycle;
		[SerializeField] private float gizmoSize = 0.5f;

		private int currentDirection;
		private Paths ownPath;
		private Hotspot ownHotspot;
		private Char occupiedCharacter;
		private bool canTurnLeft;
		private bool canTurnRight;
		private bool updateMenuHotspotLabels = true; // Set this to false to prevent updating of Navigation button hotspot labels
		private int directionOnArrive = -1;

		private static MovementNode playerNode;
		private static List<MovementNode> allNodes = new List<MovementNode>();

		#endregion


		#region UnityStandards

		private void OnEnable ()
		{
			ownPath = GetComponent <Paths>();
			ownHotspot = GetComponent <Hotspot>();

			AutoSetHotspot (false);

			if (ownHotspot != null && !string.IsNullOrEmpty (label))
			{
				ownHotspot.hotspotName = label;
			}

			nodeDirections.Sort (delegate (NodeDirection a, NodeDirection b) {return a.SpinAngle.CompareTo (b.SpinAngle);});

			EventManager.OnCharacterEndPath += OnCharacterEndPath;
			EventManager.OnOccupyPlayerStart += OnOccupyPlayerStart;
			EventManager.OnBeforeLoading += OnBeforeLoading;
			Register (this);
		}


		private void OnDisable ()
		{
			EventManager.OnCharacterEndPath -= OnCharacterEndPath;
			EventManager.OnOccupyPlayerStart -= OnOccupyPlayerStart;
			EventManager.OnBeforeLoading -= OnBeforeLoading;
			Unregister (this);
		}


		private void OnDrawGizmos ()
		{
			Gizmos.color = Color.red;

			foreach (NodeDirection nodeDirection in nodeDirections)
			{
				if (nodeDirection.LinkedNode)
				{
					Vector3 PosB = nodeDirection.LinkedNode.transform.position;
					Vector3 offset = nodeDirection.GetForward (transform, true) * gizmoSize;
					Gizmos.DrawLine (offset + transform.position, PosB);

					Gizmos.color = Color.white;
					Gizmos.DrawLine (offset + transform.position, transform.position);
				}
			}
		}

		#endregion


		#if UNITY_EDITOR

		public void DrawHandles ()
		{
			foreach (NodeDirection nodeDirection in nodeDirections)
			{
				Handles.color = nodeDirection.Color;
				Vector3 PosA = transform.position + nodeDirection.GetForward (transform, true) * gizmoSize;
				Handles.DrawLine (transform.position, PosA);
			}

			Handles.color = new Color (1f, 1f, 1f, 0.3f);
			if (canCycle)
			{
				Handles.DrawSolidArc (transform.position, Vector3.up, Vector3.forward, 360f, gizmoSize / 2f);
			}
			else
			{
				int minDirectionIndex = GetMinIndex ();
				int maxDirectionIndex = GetMaxIndex ();

				if (minDirectionIndex >= 0 && maxDirectionIndex >= 0)
				{
					Vector3 fromDirection = nodeDirections[minDirectionIndex].GetForward (transform);
					float angle = nodeDirections[maxDirectionIndex].SpinAngle - nodeDirections[minDirectionIndex].SpinAngle;
					Handles.DrawSolidArc (transform.position, Vector3.up, fromDirection, angle, gizmoSize / 2f);
				}
			}
		}


		public void ShowGUI ()
		{
			if (Application.isPlaying && occupiedCharacter != null)
			{
				EditorGUILayout.BeginVertical ("Button");
				EditorGUILayout.LabelField ("Manual movement: " + occupiedCharacter.gameObject.name, EditorStyles.boldLabel);
				GUI.enabled = canTurnLeft;
				if (GUILayout.Button ("Turn left"))
				{
					TurnLeft (false);
				}

				GUI.enabled = canTurnRight;
				if (GUILayout.Button ("Turn right"))
				{
					TurnRight (false);
				}

				GUI.enabled = CanMoveForward;
				if (GUILayout.Button ("Forward"))
				{
					MoveForward (true, false);
				}

				GUI.enabled = CanMoveBackward;
				if (GUILayout.Button ("Backward"))
				{
					MoveBackward (true, false);
				}

				GUI.enabled = true;
				EditorGUILayout.EndVertical ();
				EditorGUILayout.Space ();
			}

			EditorGUILayout.BeginVertical ("Button");
			EditorGUILayout.LabelField ("Main properties", EditorStyles.boldLabel);
			label = EditorGUILayout.TextField ("Label:", label);
			linkedPlayerStart = (PlayerStart) EditorGUILayout.ObjectField ("Linked PlayerStart:", linkedPlayerStart, typeof (PlayerStart), true);
			canCycle = EditorGUILayout.Toggle ("Can cycle?", canCycle);

			if (GetComponent<Hotspot>() != null)
			{
				autoHotspot = (MovementNodeAutoHotspot) EditorGUILayout.EnumPopup ("Auto-enable Hotspot:", autoHotspot);
			}
			gizmoSize = EditorGUILayout.Slider ("Gizmo size:", gizmoSize, 0f, 3f);
			EditorGUILayout.EndVertical ();

			EditorGUILayout.Space ();

			EditorGUILayout.BeginVertical ("Button");
			EditorGUILayout.LabelField ("Directions", EditorStyles.boldLabel);
			for (int i=0; i<nodeDirections.Count; i++)
			{
				EditorGUILayout.BeginHorizontal ();
				EditorGUILayout.LabelField ("Direction #" + nodeDirections[i].ID);

				if (GUILayout.Button ("", CustomStyles.IconCog))
				{
					SideMenu (i);
				}
				EditorGUILayout.EndHorizontal ();

				nodeDirections[i].ShowGUI (this);

				GUILayout.Box ("", GUILayout.ExpandWidth (true), GUILayout.Height (1));
			}

			if (GUILayout.Button ("Add new direction"))
			{
				int id = GetNewID ();
				nodeDirections.Add (new NodeDirection (id));
			}

			EditorGUILayout.EndVertical ();
		}


		private void SideMenu (int i)
		{
			GUI.SetNextControlName ("");
			GUI.FocusControl ("");

			selectedDirection = i;
			GenericMenu menu = new GenericMenu ();
			
			menu.AddItem (new GUIContent ("Insert after"), false, Callback, "Insert after");
			menu.AddItem (new GUIContent ("Delete"), false, Callback, "Delete");

			menu.ShowAsContext ();
		}


		private int selectedDirection;
		private void Callback (object obj)
		{
			int i = selectedDirection;
				
			switch (obj.ToString ())
			{
				case "Insert after":
					Undo.RecordObject (this, "Add direction");
					int id = GetNewID ();
					nodeDirections.Insert (i+1, new NodeDirection (id));
					break;
				
				case "Delete":
					Undo.RecordObject (this, "Delete direction");
					nodeDirections.RemoveAt (i);
					break;
			}

			EditorUtility.SetDirty (this);
		}


		private int GetNewID ()
		{
			List<int> idArray = new List<int>();
			foreach (NodeDirection nodeDirection in nodeDirections)
			{
				idArray.Add (nodeDirection.ID);
			}
			idArray.Sort ();

			int newID = 0;
			foreach (int _id in idArray)
			{
				if (newID == _id)
					newID ++;
			}
			return newID;
		}


		private int GetMinIndex ()
		{
			float minAngle = Mathf.Infinity;
			int minDirectionIndex = -1;
			
			for (int i=0; i<nodeDirections.Count; i++)
			{
				if (nodeDirections[i].isEnabled && nodeDirections[i].SpinAngle < minAngle)
				{
					minAngle = nodeDirections[i].SpinAngle;
					minDirectionIndex = i;
				}
			}
			
			return minDirectionIndex;
		}


		private int GetMaxIndex ()
		{
			float maxAngle = -1f;
			int maxDirectionIndex = -1;
			
			for (int i=0; i<nodeDirections.Count; i++)
			{
				if (nodeDirections[i].isEnabled && nodeDirections[i].SpinAngle > maxAngle)
				{
					maxAngle = nodeDirections[i].SpinAngle;
					maxDirectionIndex = i;
				}
			}
			
			return maxDirectionIndex;
		}


		public int ShowDirectionGUI (int ID, string label)
		{
			List<string> labelList = new List<string>();

			int directionNumber = -1;
			int i=0;

			if (nodeDirections != null)
			{
				foreach (NodeDirection nodeDirection in nodeDirections)
				{
					labelList.Add (nodeDirection.Label);
					
					if (nodeDirection.ID == ID)
					{
						directionNumber = i;
					}

					i++;
				}
			}
			
			if (directionNumber == -1)
			{
				if (ID != 0)
				{
					ACDebug.Log ("Previously chosen direction no longer exists!");
				}
				directionNumber = 0;
				ID = 0;
			}

			directionNumber = EditorGUILayout.Popup (label, directionNumber, labelList.ToArray ());
			return nodeDirections[directionNumber].ID;
		}

		#endif


		#region PublicFunctions

		/**
		 * <summary>Moves a character to the node.</summary>
		 * <param name = "character">The Player or NPC to move</param>
		 * <param name = "moveMethod">The way to move towards the node</param>
		 * <param name = "doPathfinding">If True, the character should make use of pathfinding to reach the node</param>
		 * <param name = "directionID">The ID number of the direction to face, if >=0</param>
		 */
		public void MoveTo (Char character, MovementNodeMoveMethod moveMethod, bool doPathfinding, int directionID = -1)
		{
			if (character == null)
			{
				return;
			}

			if (occupiedCharacter != null)
			{
				if (occupiedCharacter != character)
				{
					return;
				}
			}

			MovementNode startingNode = (moveMethod == MovementNodeMoveMethod.IgnoreOtherNodes) ? null : GetNodeWithCharacter (character);

			int nextDirection = (directionID >= 0) ? GetDirectionWithID (directionID) : -1;

			MoveOccupiedCharacter (character, false, nextDirection, doPathfinding, startingNode, (moveMethod == MovementNodeMoveMethod.MoveOneStepTowards));
		}


		/**
		 * <summary>Moves a character to the node instantly.</summary>
		 * <param name = "character">The Player or NPC to move</param>
		 * <param name = "directionID">The ID number of the direction to face, if >=0</param>
		 */
		public void SnapTo (Char character, int directionID = -1)
		{
			if (character == null)
			{
				return;
			}

			if (occupiedCharacter != null)
			{
				if (occupiedCharacter != character)
				{
					return;
				}
			}

			int nextDirection = (directionID >= 0) ? GetDirectionWithID (directionID) : -1;

			MoveOccupiedCharacter (character, true, nextDirection);
		}


		/**
		 * <summary>Marks the node as unoccupied by any character.</summary>
		 * <param name = "_character">If set, the node will only be marked as unoccupied if it is currently occupied by this character</param>
		 */
		public void Unoccupy (Char _character = null)
		{
			if (_character == null || _character == occupiedCharacter)
			{
				if (occupiedCharacter is Player && playerNode == this)
				{
					playerNode = null;
				}

				occupiedCharacter = null;
			}
		}


		/**
		 * <summary>Causes the node's occupied character to move forward, if possible.</summary>
		 * <param name = "doPathfinding">If True, the character will make use of pathfinding to move</param>
		 * <param name = "isInstant">If True, the character will teleport to the next node</param>
		 */
		public void MoveForward (bool doPathfinding, bool isInstant)
		{
			if (occupiedCharacter != null && CanMoveForward)
			{
				MovementNode forwardNode = nodeDirections[currentDirection].LinkedNode;

				if (isInstant)
				{
					forwardNode.SnapTo (occupiedCharacter);
				}
				else
				{
					forwardNode.MoveTo (occupiedCharacter, MovementNodeMoveMethod.IgnoreOtherNodes, doPathfinding);
				}
			}
		}


		/**
		 * <summary>Checks if the node's occupied character is able to move forward.</summary>
		 * <returns>True if the node's occupied character is able to move forward.</returns>
		 */
		public bool CanMoveForward
		{
			get
			{
				return IsDirectionValid (nodeDirections[currentDirection]);
			}
		}


		/**
		 * <summary>Causes the node's occupied character to move backward, if possible.</summary>
		 * <param name = "doPathfinding">If True, the character will make use of pathfinding to move</param>
		 * <param name = "isInstant">If True, the character will teleport to the next node</param>
		 */
		public void MoveBackward (bool doPathfinding, bool isInstant)
		{
			if (occupiedCharacter != null && CanMoveBackward)
			{
				MovementNode backwardNode = GetBackwardNodeDirection ().LinkedNode;

				if (isInstant)
				{
					backwardNode.SnapTo (occupiedCharacter);
				}
				else
				{
					backwardNode.MoveTo (occupiedCharacter, MovementNodeMoveMethod.IgnoreOtherNodes, doPathfinding);
				}
			}
		}


		/**
		 * <summary>Checks if the node's occupied character is able to move backward.</summary>
		 * <returns>True if the node's occupied character is able to move backward.</returns>
		 */
		public bool CanMoveBackward
		{
			get
			{
				NodeDirection backDirection = GetBackwardNodeDirection ();
				return (backDirection != null && IsDirectionValid (backDirection));
			}
		}


		/**
		 * <summary>Causes the node's occupied character to turn left.</summary>
		 * <param name = "isInstant">If True, the character will turn instantly</param>
		 */
		public void TurnLeft (bool isInstant)
		{
			if (occupiedCharacter != null && canTurnLeft)
			{
				int nextDirection = FindPreviousDirection ();
				if (nextDirection != currentDirection)
				{
					SetDirection (nextDirection, isInstant);
				}
			}
		}


		/**
		 * <summary>Causes the node's occupied character to turn right.</summary>
		 * <param name = "isInstant">If True, the character will turn instantly</param>
		 */
		public void TurnRight (bool isInstant)
		{
			if (occupiedCharacter != null && canTurnRight)
			{
				int nextDirection = FindNextDirection ();
				if (nextDirection != currentDirection)
				{
					SetDirection (nextDirection, isInstant);
				}
			}
		}


		/**
		 * <summary>Gets the label of the direction that is to the left of the current direction.</summary>
		 * <returns>The label of the direction that is to the left of the current direction.</returns>
		 */
		public string GetLeftDirectionLabel ()
		{
			int i = FindPreviousDirection ();
			return nodeDirections[i].Label;
		}


		/**
		 * <summary>Gets the label of the direction that is to the right of the current direction.</summary>
		 * <returns>The label of the direction that is to the right of the current direction.</returns>
		 */
		public string GetRightDirectionLabel ()
		{
			int i = FindNextDirection ();
			return nodeDirections[i].Label;
		}


		/**
		 * <summary>Gets the label of the MovementNode that is ahead the current direction.</summary>
		 * <returns>The label of the MovementNode that is ahead of the current direction.</returns>
		 */
		public string GetForwardNodeLabel ()
		{
			MovementNode forwardNode = nodeDirections[currentDirection].LinkedNode;
			if (forwardNode != null)
			{
				return forwardNode.Label;
			}
			return string.Empty;
		}


		/**
		 * <summary>Gets the label of the MovementNode that is behind the current direction.</summary>
		 * <returns>The label of the MovementNode that is behind of the current direction.</returns>
		 */
		public string GetBackwardNodeLabel ()
		{
			NodeDirection backDirection = GetBackwardNodeDirection ();
			if (backDirection != null && backDirection.LinkedNode != null)
			{
				return backDirection.LinkedNode.Label;
			}
			return string.Empty;
		}


		/**
		 * <summary>Sets the enabled state of one of the node's directions.</summary>
		 * <param name = "ID">The unique identified of the direction to modify</param>
		 * <param name = "state">If True, the direction will be enabled. If false, it will be disabled</param>
		 */
		public void SetDirectionState (int ID, bool state)
		{
			for (int i=0; i<nodeDirections.Count; i++)
			{
				if (nodeDirections[i].ID == ID)
				{
					nodeDirections[i].isEnabled = state;
				}
			}
		}


		/**
		 * <summary>Enables a given direction.</summary>
		 * <param name = "directionID">The ID number of the direction to affect</param>
		 */
		public void EnableDirection (int directionID)
		{
			foreach (NodeDirection direction in nodeDirections)
			{
				if (direction.ID == directionID)
				{
					direction.isEnabled = true;
				}
			}
		}


		/**
		 * <summary>Disables a given direction.</summary>
		 * <param name = "directionID">The ID number of the direction to affect</param>
		 */
		public void DisableDirection (int directionID)
		{
			foreach (NodeDirection direction in nodeDirections)
			{
				if (direction.ID == directionID)
				{
					direction.isEnabled = false;
				}
			}
		}


		/**
		 * <summary>Saves data related to the node.</summary>
		 * <param name = "data">The MovementNodeData class to update with node data</param>
		 * <returns>A MovementNodeData class, updated with current node data.</returns>
		 */
		public MovementNodeData SaveData (MovementNodeData data)
		{
			StringBuilder stateString = new StringBuilder ();
			foreach (NodeDirection nodeDirection in nodeDirections)
			{
				stateString.Append (nodeDirection.GetSaveString ());
				stateString.Append (SaveSystem.pipe);
			}
			data.directionStates = stateString.ToString ();

			data.occupiedByPlayer = (playerNode == this);
			data.occupiedCharacterID = 0;
			if (!data.occupiedByPlayer && occupiedCharacter != null)
			{
				data.occupiedCharacterID = Serializer.GetConstantID (occupiedCharacter.gameObject);
			}

			return data;
		}


		/**
		 * <summary>Restores the node's state from saved data</summary>
		 * <param name = "data">The data class to load from</param>
		 */
		public void LoadData (MovementNodeData data)
		{
			if (!string.IsNullOrEmpty (data.directionStates))
			{
				string[] valuesArray = data.directionStates.Split (SaveSystem.pipe[0]);

				if (valuesArray != null && valuesArray.Length > 0)
				{
					foreach (string value in valuesArray)
					{
						string[] dataChunks = value.Split (SaveSystem.colon[0]);
						if (dataChunks != null && dataChunks.Length == 2)
						{
							int _id = -1;
							if (int.TryParse (dataChunks[0], out _id))
							{
								foreach (NodeDirection nodeDirection in nodeDirections)
								{
									if (nodeDirection.ID == _id)
									{
										int _enabled = -1;
										if (int.TryParse (dataChunks[1], out _enabled))
										{
											nodeDirection.isEnabled = (_enabled == 1);
										}
									}
								}
							}
						}
					}
				}
			}

			occupiedCharacter = null;
			if (data.occupiedByPlayer)
			{
				occupiedCharacter = KickStarter.player;
			}
			else if (data.occupiedCharacterID != 0)
			{
				occupiedCharacter = Serializer.returnComponent <Char> (data.occupiedCharacterID);
			}

			if (occupiedCharacter != null)
			{
				SnapTo (occupiedCharacter);
			}
		}

		#endregion


		#region PrivateFunctions

		private void OccupyCharacter (Char character)
		{
			occupiedCharacter = character;
			if (character is Player)
			{
				playerNode = this;
			}

			foreach (MovementNode node in allNodes)
			{
				if (node != this)
				{
					node.Unoccupy (occupiedCharacter);
				}
			}
			UpdateAutoHotspots ();
		}


		private void MoveOccupiedCharacter (Char character, bool isInstant, int directionIndex, bool doPathfinding = false, MovementNode startingNode = null, bool firstOnly = false)
		{
			MovementNode[] nodePath = (startingNode != null) ? MovementNode.GetShortestPath (startingNode, this) : null;

			if (nodePath != null && nodePath.Length == 1 && nodePath[0] != startingNode)
			{
				// Not connected, try enabled
				nodePath = MovementNode.GetShortestPath (startingNode, this, true);
				if (nodePath != null && nodePath.Length == 1 && nodePath[0] != startingNode)
				{
					ACDebug.LogWarning ("Cannot navigate from " + startingNode + " to " + this + ", as the nodes are not connected.");
					return;
				}
			}

			OccupyCharacter (character);

			if (isInstant)
			{
				character.Teleport (transform.position);
				SetDirection (directionIndex, true);
			}
			else
			{
				ownPath.nodes.Clear ();

				if (startingNode != null && nodePath != null)
				{
					List<Vector3> targetPoints = new List<Vector3>();
					foreach (MovementNode node in nodePath)
					{
						if (node != startingNode)
						{
							targetPoints.Add (node.transform.position);

							if (firstOnly)
							{
								node.OccupyCharacter (character);
								break;
							}
						}
					}

					Vector3[] pointArray = (doPathfinding)
											? KickStarter.navigationManager.navigationEngine.GetPointsArray (startingNode.transform.position, targetPoints.ToArray ())
											: targetPoints.ToArray ();

					foreach (Vector3 point in pointArray)
					{
						ownPath.nodes.Add (point);
					}
				}
				else if (doPathfinding)
				{
					ownPath.RecalculateToCenter (character.transform.position);
				}

				if (ownPath.nodes.Count == 0)
				{
					ownPath.nodes.Add (transform.position);
				}

				directionOnArrive = directionIndex;

				if (character is Player)
				{
					(character as Player).SetTilt (0f, false);
				}
				character.SetPath (ownPath);

				canTurnLeft = false;
				canTurnLeft = false;
			}
		}


		private void SetDirection (int index, bool isInstant)
		{
			currentDirection = (index >= 0) ? index : GetBestDirection ();
			if (nodeDirections != null && nodeDirections.Count > currentDirection)
			{
				occupiedCharacter.SetLookDirection (nodeDirections[currentDirection].GetForward (transform), isInstant);

				if (occupiedCharacter is Player)
				{
					(occupiedCharacter as Player).SetTilt (nodeDirections[currentDirection].PitchAngle, isInstant);
				}

				CalculatePossibleTurns ();
			}
		}


		private void CalculatePossibleTurns ()
		{
			canTurnLeft = (FindPreviousDirection () != currentDirection);
			canTurnRight = (FindNextDirection () != currentDirection);

			if (occupiedCharacter != null && occupiedCharacter is Player)
			{
				UpdateNavigationMenu ();
			}
			UpdateAutoHotspots ();
		}


		private void UpdateNavigationMenu ()
		{
			Menu navigationMenu = PlayerMenus.GetMenuWithName ("Navigation");
			if (navigationMenu != null)
			{
				MenuButton turnLeftButton = navigationMenu.GetElementWithName ("TurnLeft") as MenuButton;
				if (turnLeftButton != null)
				{
					turnLeftButton.IsVisible = MovementNode.PlayerNode.CanTurnLeft;
					if (updateMenuHotspotLabels && MovementNode.PlayerNode.CanTurnLeft)
					{
						turnLeftButton.hotspotLabel = MovementNode.PlayerNode.GetLeftDirectionLabel ();
					}
				}

				MenuButton turnRightButton = navigationMenu.GetElementWithName ("TurnRight") as MenuButton;
				if (turnRightButton != null)
				{
					turnRightButton.IsVisible = MovementNode.PlayerNode.CanTurnRight;
					if (updateMenuHotspotLabels && MovementNode.PlayerNode.CanTurnRight)
					{
						turnRightButton.hotspotLabel = MovementNode.PlayerNode.GetRightDirectionLabel ();
					}
				}

				MenuButton moveForwardButton = navigationMenu.GetElementWithName ("MoveForward") as MenuButton;
				if (moveForwardButton != null)
				{
					moveForwardButton.IsVisible = MovementNode.PlayerNode.CanMoveForward;
					if (updateMenuHotspotLabels && MovementNode.PlayerNode.CanMoveForward)
					{
						moveForwardButton.hotspotLabel = MovementNode.PlayerNode.GetForwardNodeLabel ();
					}
				}

				MenuButton moveBackwardButton = navigationMenu.GetElementWithName ("MoveBackward") as MenuButton;
				if (moveForwardButton != null)
				{
					moveBackwardButton.IsVisible = MovementNode.PlayerNode.CanMoveBackward;
					if (updateMenuHotspotLabels && MovementNode.PlayerNode.CanMoveBackward)
					{
						moveBackwardButton.hotspotLabel = MovementNode.PlayerNode.GetBackwardNodeLabel ();
					}
				}
			}
		}


		private void AutoSetHotspot (bool state)
		{
			if (ownHotspot != null && autoHotspot != MovementNodeAutoHotspot.Never)
			{
				if (state)
				{
					ownHotspot.TurnOn ();
				}
				else
				{
					ownHotspot.TurnOff ();
				}
			}
		}


		private int FindPreviousDirection ()
		{
			if (nodeDirections == null || nodeDirections.Count < 2) return currentDirection;

			int i = currentDirection;
			int numIterations = 0;

			float currentAngle = nodeDirections[currentDirection].SpinAngle; // 0 -> 360
			float minPreviousAngle = (canCycle) ? -Mathf.Infinity : (currentAngle - 180f); // -180 -> 180

			while (numIterations < nodeDirections.Count)
			{
				i --;
				numIterations ++;

				if (i < 0)
				{
					i = nodeDirections.Count - 1;
				}

				if (nodeDirections[i].isEnabled)
				{
					float previousAngle = nodeDirections[i].SpinAngle; // 0 -> 360

					if (previousAngle > minPreviousAngle + 360f) previousAngle -= 360f;
					if (previousAngle > minPreviousAngle + 360f) previousAngle -= 360f;

					if (canCycle)
					{
						return i;
					}
					if (previousAngle > minPreviousAngle && 
						Mathf.Abs (previousAngle - minPreviousAngle) < 180f)
					{
						return i;
					}
				}
			}

			return currentDirection;
		}


		private int FindNextDirection ()
		{
			if (nodeDirections == null || nodeDirections.Count < 2) return currentDirection;

			int i = currentDirection;
			int numIterations = 0;

			float currentAngle = nodeDirections[currentDirection].SpinAngle; // 0 -> 360
			float maxNextAngle = (canCycle) ? Mathf.Infinity : (currentAngle + 180f); // 180 -> 540

			while (numIterations < nodeDirections.Count)
			{
				i ++;
				numIterations ++;

				if (i >= nodeDirections.Count)
				{
					i = 0;
				}

				if (nodeDirections[i].isEnabled)
				{
					float nextAngle = nodeDirections[i].SpinAngle; // 0 -> 360

					if (nextAngle < maxNextAngle - 360f) nextAngle += 360f;
					if (nextAngle < maxNextAngle - 360f) nextAngle += 360f;

					if (canCycle)
					{
						return i;
					}
					if (nextAngle < maxNextAngle && 
						Mathf.Abs (nextAngle - maxNextAngle) < 180f)
					{
						return i;
					}
				}
			}

			return currentDirection;
		}


		private int GetBestDirection ()
		{
			// Find right direction to start with automatically
			Vector3 referenceForward = occupiedCharacter.transform.forward;

			int bestCandidateIndex = 0;
			float bestDotProduct = -Mathf.Infinity;

			for (int i=0; i<nodeDirections.Count; i++)
			{
				float dotProduct = Vector3.Dot (referenceForward, nodeDirections[i].GetForward (transform));
				if (dotProduct > bestDotProduct)
				{
					bestDotProduct = dotProduct;
					bestCandidateIndex = i;
				}
			}

			return bestCandidateIndex;
		}


		private int GetDirectionWithID (int ID)
		{
			for (int i=0; i<nodeDirections.Count; i++)
			{
				if (nodeDirections[i].ID == ID)
				{
					return i;
				}
			}
			ACDebug.Log (gameObject.name + " - cannot find direction with ID=" + ID, gameObject);
			return 0;
		}


		private bool IsDirectionValid (NodeDirection direction)
		{
			return (direction.isEnabled && direction.LinkedNode != null && !direction.LinkedNode.IsOccupied);
		}


		private NodeDirection GetBackwardNodeDirection ()
		{
			float forwardAngle = nodeDirections[currentDirection].SpinAngle;
			float bestBackAngle = forwardAngle + 180f; // 180 -> 540
			if (bestBackAngle > 360f) bestBackAngle -= 360f; // 0 -> 360 //////

			float min = Mathf.Infinity;
			int closestIndex = -1;

			for (int i=0; i<nodeDirections.Count; i++)
			{
				float angleToCheck = nodeDirections[i].SpinAngle; // 0 -> 360
				float angleDiff = Mathf.Abs (angleToCheck - bestBackAngle); // 0 -> 360
				if (angleDiff > 180f) angleDiff = 360f - angleDiff; // 0 -> 180
				if (angleDiff >= 90f) continue; // 0 -> 90

				if (angleDiff < min)
				{
					min = angleDiff;
					closestIndex = i;
				}
			}

			if (closestIndex >= 0)
			{
				return nodeDirections[closestIndex];
			}
			return null;
		}


		private void OnCharacterEndPath (Char character, Paths path)
		{
			if (path == ownPath && character == occupiedCharacter)
			{
				int nextDirection = (directionOnArrive >= 0) ? directionOnArrive : GetBestDirection ();
				directionOnArrive = -1;

				SetDirection (nextDirection, false);
			}
		}


		private void OnOccupyPlayerStart (Player player, PlayerStart playerStart)
		{
			if (playerStart == linkedPlayerStart)
			{
				SnapTo (player);
			}
		}


		private void OnBeforeLoading (SaveFile saveFile)
		{
			playerNode = null;
		}

		#endregion


		#region StaticFunctions

		/**
		 * The node that the Player is occupying.
		 */
		public static MovementNode PlayerNode
		{
			get
			{
				return playerNode;
			}
		}


		/**
		 * <summary>Moves a character forward, if they are on a node and able.</summary>
		 * <param name = "_character">The Player or NPC to move</param>
		 * <param name = "doPathfinding">If True, the character will rely on pathfinding to move</param>
		 * <param name = "isInstant">If True, the character will teleport instantly</param>
		 */
		public static void MoveForward (Char _character, bool doPathfinding, bool isInstant)
		{
			MovementNode node = GetNodeWithCharacter (_character);
			if (node != null)
			{
				node.MoveForward (doPathfinding, isInstant);
			}
		}


		/**
		 * <summary>Moves a character backward, if they are on a node and able.</summary>
		 * <param name = "_character">The Player or NPC to move</param>
		 * <param name = "doPathfinding">If True, the character will rely on pathfinding to move</param>
		 * <param name = "isInstant">If True, the character will teleport instantly</param>
		 */
		public static void MoveBackward (Char _character, bool doPathfinding, bool isInstant)
		{
			MovementNode node = GetNodeWithCharacter (_character);
			if (node != null)
			{
				node.MoveBackward (doPathfinding, isInstant);
			}
		}


		/**
		 * <summary>Turns a character right, if they are on a node and able.</summary>
		 * <param name = "_character">The Player or NPC to turn</param>
		 * <param name = "isInstant">If True, the character will turn instantly</param>
		 */
		public static void TurnRight (Char _character, bool isInstant)
		{
			MovementNode node = GetNodeWithCharacter (_character);
			if (node != null)
			{
				node.TurnRight (isInstant);
			}
		}


		/**
		 * <summary>Turns a character left, if they are on a node and able.</summary>
		 * <param name = "_character">The Player or NPC to turn</param>
		 * <param name = "isInstant">If True, the character will turn instantly</param>
		 */
		public static void TurnLeft (Char _character, bool isInstant)
		{
			MovementNode node = GetNodeWithCharacter (_character);
			if (node != null)
			{
				node.TurnLeft (isInstant);
			}
		}


		/**
		 * <summary>Registers a MovementNode, so that it can be updated</summary>
		 * <param name = "_object">The MovementNode to register</param>
		 */
		public static void Register (MovementNode _object)
		{
			if (!allNodes.Contains (_object))
			{
				allNodes.Add (_object);
			}
		}
		

		/**
		 * <summary>Unregisters a MovementNode, so that it is no longer updated</summary>
		 * <param name = "_object">The MovementNode to unregister</param>
		 */
		public static void Unregister (MovementNode _object)
		{
			if (allNodes.Contains (_object))
			{
				allNodes.Remove (_object);
			}
		}


		private static void UpdateAutoHotspots ()
		{
			foreach (MovementNode node in allNodes)
			{
				if (node.IsOccupied)
				{
					node.AutoSetHotspot (false);
				}
				else if (node.autoHotspot == MovementNodeAutoHotspot.Always)
				{
					node.AutoSetHotspot (true);
				}
				else if (node.autoHotspot == MovementNodeAutoHotspot.OnlyImmediate)
				{
					bool enable = false;

					foreach (NodeDirection nodeDirection in PlayerNode.nodeDirections)
					{
						if (nodeDirection.isEnabled && nodeDirection.LinkedNode == node)
						{
							enable = true;
							break;
						}
					}

					node.AutoSetHotspot (enable);
				}
			}
		}


		private static MovementNode GetNodeWithCharacter (Char _character)
		{
			if (_character != null)
			{
				foreach (MovementNode node in allNodes)
				{
					if (node.occupiedCharacter == _character)
					{
						return node;
					}
				}
			}
			return null;
		}


		private static MovementNode[] GetShortestPath (MovementNode start, MovementNode end, bool toNearestEnabled = false)
		{
			if (start == null || end == null)
			{
				return null;
			}
			
			List<MovementNode> path = new List<MovementNode>();

			if (start == end)
			{
				path.Add (start);
				return path.ToArray ();
			}
			
			List<MovementNode> unvisited = new List<MovementNode>();
			Dictionary<MovementNode, MovementNode> previous = new Dictionary<MovementNode, MovementNode> ();
			Dictionary<MovementNode, float> distances = new Dictionary<MovementNode, float> ();
			
			for (int i=0; i<allNodes.Count; i++)
			{
				MovementNode node = allNodes[i];
				
				unvisited.Add (node);
				distances.Add (node, float.MaxValue);
			}

			distances[start] = 0f;
			while (unvisited.Count != 0)
			{
				unvisited = unvisited.OrderBy (node => distances[node]).ToList ();
				
				MovementNode current = unvisited[0];
				
				unvisited.Remove (current);
				
				if (current == end)
				{
					while (previous.ContainsKey (current))
					{
						path.Insert (0, current);
						current = previous[current];
					}
					
					path.Insert (0, current);
					break;
				}
				
				for (int i=0; i<current.nodeDirections.Count; i++)
				{
					MovementNode neighbour = current.nodeDirections[i].LinkedNode;
					if (neighbour == null ||
						(!toNearestEnabled && !current.nodeDirections[i].isEnabled) ||
						(!toNearestEnabled && current.nodeDirections[i].LinkedNode != null && current.nodeDirections[i].LinkedNode.IsOccupied))
					{
						continue;
					}

					float length = Vector3.Distance (current.transform.position, neighbour.transform.position);
					float alt = distances[current] + length;
					
					if (alt < distances[neighbour])
					{
						distances[neighbour] = alt;
						previous[neighbour] = current;
					}
				}
			}

			if (!toNearestEnabled || path.Count <= 1)
			{
				return path.ToArray ();
			}

			List<MovementNode> nearestPath = new List<MovementNode>();
			nearestPath.Add (path[0]);

			for (int i=0; i<path.Count-1; i++)
			{
				MovementNode thisNode = path[i];
				MovementNode nextNode = path[i+1];

				bool foundLink = false;
				foreach (NodeDirection direction in thisNode.nodeDirections)
				{
					if (direction.LinkedNode == nextNode && direction.isEnabled && !nextNode.IsOccupied)
					{
						foundLink = true;
					}
				}

				if (foundLink)
				{
					nearestPath.Add (nextNode);
				}
			}
			return nearestPath.ToArray ();
		}

		#endregion


		#region GetSet

		/**
		 * The node's label, or - if not set - the GameObject's name
		 */
		public string Label
		{
			get
			{
				if (string.IsNullOrEmpty (label))
				{
					return gameObject.name;
				}
				return label;
			}
		}

		/**
		 * True if the node is occupied by a Player or NPC.
		 */
		public bool IsOccupied
		{
			get
			{
				return (occupiedCharacter != null);
			}
		}


		/**
		 * True if it is possible to turn left.
		 */
		public bool CanTurnLeft
		{
			get
			{
				return canTurnLeft;
			}
		}


		/**
		 * True if it is possible to turn right.
		 */
		public bool CanTurnRight
		{
			get
			{
				return canTurnRight;
			}
		}

		#endregion


		#region PrivateClasses

		[System.Serializable]
		private class NodeDirection
		{

			[SerializeField] public bool isEnabled;
			[SerializeField] private string label;
			[SerializeField] private int id;
			[SerializeField] [Range(0f, 360f)] private float spinAngle;
			[SerializeField] [Range(-60f, 60f)] private float pitchAngle;			
			[SerializeField] private MovementNode linkedNode;
			[SerializeField] private Color color = Color.white;


			public NodeDirection (int _id)
			{
				id = _id;
				label = "Direction " + id.ToString ();
				isEnabled = true;
				spinAngle = 180f;
				pitchAngle = 0f;
				linkedNode = null;
				color = Color.white;
			}


			public MovementNode LinkedNode
			{
				get
				{
					return linkedNode;
				}
			}


			public int ID
			{
				get
				{
					return id;
				}
			}


			public Color Color
			{
				get
				{
					if (!isEnabled)
					{
						return new Color (color.r, color.g, color.b, color.a / 2f);
					}
					return color;
				}
			}


			public string Label
			{
				get
				{
					if (string.IsNullOrEmpty (label))
					{
						label = "Direction " + id.ToString ();
					}
					return label;
				}
			}


			public float SpinAngle
			{
				get
				{
					return spinAngle;
				}
			}


			public float PitchAngle
			{
				get
				{
					return pitchAngle;
				}
			}


			public Vector3 GetForward (Transform transform, bool accountForPitch = false)
			{
				Quaternion rotation = transform.rotation * Quaternion.Euler (Vector3.up * spinAngle);

				if (accountForPitch)
				{
					rotation *= Quaternion.Euler (transform.right * pitchAngle);
				}

				return rotation * Vector3.forward;
			}


			public string GetSaveString ()
			{
				return id.ToString () + SaveSystem.colon + ((isEnabled) ? "1" : "0");
			}


			#if UNITY_EDITOR

			public void ShowGUI (MovementNode _target)
			{
				label = EditorGUILayout.TextField ("Label", label);
				isEnabled = EditorGUILayout.Toggle ("Is enabled?", isEnabled);
				spinAngle = EditorGUILayout.Slider ("Spin angle:", spinAngle, 0f, 359f);
				pitchAngle = EditorGUILayout.Slider ("Pitch angle:", pitchAngle, -60f, 60f);
				color = EditorGUILayout.ColorField ("Gizmo color:", color);
				linkedNode = (MovementNode) EditorGUILayout.ObjectField ("Linked node:", linkedNode, typeof (MovementNode), true);

				if (linkedNode == _target)
				{
					EditorGUILayout.HelpBox ("A node cannot link to itself!", MessageType.Warning);
				}
			}

			#endif

			#endregion

		}

	}

}