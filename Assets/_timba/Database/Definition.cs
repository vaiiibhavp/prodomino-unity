using UnityEngine;

namespace Timba.Database
{
    /// <summary>A definition is one entry in a Database and is identified by an ID</summary>
    [System.Serializable]
	public abstract class Definition : ScriptableObject
	{
		public string definitionId;

		public abstract string FileName { get; }
	}

}
