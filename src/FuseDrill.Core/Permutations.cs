namespace FuseDrill.Core
{
    public static class PermutationGenerator
    {
        // Method to generate permutations of a list
        public static List<List<T>> GetPermutations<T>(List<T> list)
        {
            if (list == null || list.Count == 0)
                return [];

            var result = new List<List<T>>();
            Permute(list, 0, result);
            return result;
        }

        // Method to generate all permutations of 2 items from a list
        public static List<List<T>> GetPermutationsOfTwo<T>(List<T> list)
        {
            var result = new List<List<T>>();

            if (list == null || list.Count < 2)
                return result;

            for (int i = 0; i < list.Count; i++)
            {
                for (int j = 0; j < list.Count; j++)
                {
                    if (i != j)
                    {
                        result.Add(new List<T> { list[i], list[j] });
                    }
                }
            }

            return result;
        }


        // Method to generate all permutations of 1 items from a list
        public static List<List<T>> GetPermutationsOfOne<T>(List<T> list)
        {
            return new List<List<T>> { list };
        }

        // Helper method to generate permutations recursively
        private static void Permute<T>(List<T> list, int start, List<List<T>> result)
        {
            if (start >= list.Count)
            {
                // Add a copy of the current permutation to the result
                result.Add(new List<T>(list));
                return;
            }

            for (int i = start; i < list.Count; i++)
            {
                // Swap the current element with the starting element
                Swap(list, start, i);
                // Recurse with the next element
                Permute(list, start + 1, result);
                // Swap back to restore the original list
                Swap(list, start, i);
            }
        }

        // Helper method to swap elements in a list
        private static void Swap<T>(List<T> list, int index1, int index2)
        {
            T temp = list[index1];
            list[index1] = list[index2];
            list[index2] = temp;
        }
    }
}
