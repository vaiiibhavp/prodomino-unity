using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Timba.Database
{
	/// <summary>A Database is serializable collection of Definitions</summary>
	public abstract class Database<T> : ScriptableObject, IEnumerable<T> where T : Definition, new()
	{
		[SerializeField] protected List<T> m_definitions;

		private Dictionary<string, T> m_definitionLookUp;

		public Database()
		{
			m_definitions = new List<T>();
		}

		public int Count
		{
			get { return m_definitions.Count; }
		}

		public List<T> Definitions
		{
			get { return m_definitions; }
		}

		protected T GetDefinitionByIndex(int index)
		{
			if (index >= 0 && index <= Count)
			{
				return m_definitions[index];
			}

			UnityEngine.Debug.LogWarning(string.Format("{0} has {1} definitions but you're looking for index {2}", name,
				Count, index));

			return default(T);
		}

		public T this[int id]
		{
			get { return GetDefinitionByIndex(id); }
		}

		public T GetDefinitionById(string id, bool showWarnings = true)
		{
			if (m_definitions == null || m_definitions.Count == 0)
			{
				return null;
			}

			// First time lookup needs to create lookup table
			if (m_definitionLookUp == null)
			{
				CreateStringIdLookup();
			}

			if (string.IsNullOrEmpty(id))
			{
				return null;
			}

			T value = null;
			if (!m_definitionLookUp.TryGetValue(id, out value) && showWarnings)
			{
				Debug.LogWarningFormat("Could not find definition with string ID {0} in {1}", id, name);
			}

			return value;
		}

		public void CreateStringIdLookup()
		{
			if (m_definitions == null || m_definitions.Count == 0 || // No definitions
			    m_definitionLookUp != null) // Already made
			{
				return;
			}

			m_definitionLookUp = new Dictionary<string, T>(m_definitions.Count);
			for (int i = 0; i < m_definitions.Count; ++i)
			{
				if (m_definitionLookUp.ContainsKey(m_definitions[i].definitionId))
				{
					Debug.LogErrorFormat("{0} already contains key: {1}. Why are you trying to add it again?", name,
						m_definitions[i].definitionId);
				}
				else
				{
					m_definitionLookUp[m_definitions[i].definitionId] = m_definitions[i];
				}
			}
		}

		#region IEnumerable Members

		public IEnumerator<T> GetEnumerator()
		{
			return m_definitions.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return m_definitions.GetEnumerator();
		}

		#endregion
	}
}