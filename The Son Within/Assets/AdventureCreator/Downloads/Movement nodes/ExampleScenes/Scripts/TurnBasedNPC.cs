using UnityEngine;
using System.Collections;
using AC;
using AC.MovementNodes;

public class TurnBasedNPC : MonoBehaviour
{

	public MovementNode targetNode;
	private NPC npc;


	private void OnEnable ()
	{
		npc = GetComponent <NPC>();
		EventManager.OnCharacterEndPath += My_OnCharacterEndPath;
	}


	private void OnDisable ()
	{
		EventManager.OnCharacterEndPath -= My_OnCharacterEndPath;
	}


	private void My_OnCharacterEndPath (AC.Char character, Paths path)
	{
		if (character is Player)
		{
			if (!targetNode.IsOccupied)
			{
				KickStarter.stateHandler.EnforceCutsceneMode = true;
				targetNode.MoveTo (npc, MovementNodeMoveMethod.MoveOneStepTowards, false);
			}
		}
		else if (character == npc)
		{
			KickStarter.stateHandler.EnforceCutsceneMode = false;
		}
	}


}
