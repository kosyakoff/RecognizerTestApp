﻿using System;

namespace RecognizerTestApp
{
    public static class StringDistance
    {
        public static int GetDamerauLevenshteinDistance(string s, string t)
        {
            var bounds = new {Height = s.Length + 1, Width = t.Length + 1};

            var matrix = new int[bounds.Height, bounds.Width];

            for (var height = 0; height < bounds.Height; height++) matrix[height, 0] = height;
            ;
            for (var width = 0; width < bounds.Width; width++) matrix[0, width] = width;
            ;

            for (var height = 1; height < bounds.Height; height++)
            for (var width = 1; width < bounds.Width; width++)
            {
                var cost = s[height - 1] == t[width - 1] ? 0 : 1;
                var insertion = matrix[height, width - 1] + 1;
                var deletion = matrix[height - 1, width] + 1;
                var substitution = matrix[height - 1, width - 1] + cost;

                var distance = Math.Min(insertion, Math.Min(deletion, substitution));

                if (height > 1 && width > 1 && s[height - 1] == t[width - 2] && s[height - 2] == t[width - 1])
                    distance = Math.Min(distance, matrix[height - 2, width - 2] + cost);

                matrix[height, width] = distance;
            }

            return matrix[bounds.Height - 1, bounds.Width - 1];
        }
    }
}