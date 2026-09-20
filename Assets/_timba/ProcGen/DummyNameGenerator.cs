using UnityEngine;

namespace Timba.ProcGen
{
    public class DummyNameGenerator
    {
        private static string[] names;

        static DummyNameGenerator()
        {
            names = Resources.Load<TextAsset>("dummyNames")?.text?.Split('\n');

            // Limpia cada nombre
            for (int i = 0; i < names.Length; i++)
                names[i] = names[i].Trim(); // Remueve \r, espacios, tabs, etc.
        }

        public static string GetRandomName()
        {
            if (names == null || names.Length == 0)
                return "Unknown";

            return names[Random.Range(0, names.Length)];
        }
    }
}
