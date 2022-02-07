using UnityEngine;
using UnityEditor;
using System.Collections;

namespace AC.MovementNodes
{

	[CustomEditor(typeof(MovementNode))]
	public class MovementNodeEditor : Editor
	{

		public override void OnInspectorGUI ()
		{
			MovementNode _target = (MovementNode) target;

			_target.ShowGUI ();

			if (GUI.changed)
			{
				UnityVersionHandler.CustomSetDirty (_target, true);
			}
		}


		private void OnSceneGUI ()
		{
			MovementNode _target = (MovementNode) target;

			_target.DrawHandles ();
		}

	}

}