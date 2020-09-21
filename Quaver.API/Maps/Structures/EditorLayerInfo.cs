﻿using System;
using System.Collections.Generic;
using System.Drawing;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;
using YamlDotNet.Serialization;

namespace Quaver.API.Maps.Structures
{
    [Serializable]
    [MoonSharpUserData]
    public class EditorLayerInfo
    {
        /// <summary>
        ///     The name of the layer
        /// </summary>
        public string Name { get; [MoonSharpVisible(false)] set; }

        /// <summary>
        ///     Is the layer hidden in the editor?
        /// </summary>
        public bool Hidden { get; [MoonSharpVisible(false)] set; }

        /// <summary>
        ///     The color of the layer (default is white)
        /// </summary>
        public string ColorRgb { get; [MoonSharpVisible(false)] set; }

        /// <summary>
        ///     Converts the stringified color to a System.Drawing color
        /// </summary>
        /// <returns></returns>
        [MoonSharpVisible(false)]
        public Color GetColor()
        {
            if (ColorRgb == null)
                return Color.White;

            var split = ColorRgb.Split(',');

            try
            {
                return Color.FromArgb(byte.Parse(split[0]), byte.Parse(split[1]), byte.Parse(split[2]));
            }
            catch (Exception)
            {
                return Color.White;
            }
        }

        /// <summary>
        ///     By-value comparer, auto-generated by Rider.
        /// </summary>
        private sealed class ByValueEqualityComparer : IEqualityComparer<EditorLayerInfo>
        {
            public bool Equals(EditorLayerInfo x, EditorLayerInfo y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (ReferenceEquals(x, null)) return false;
                if (ReferenceEquals(y, null)) return false;
                if (x.GetType() != y.GetType()) return false;
                return string.Equals(x.Name, y.Name) && x.Hidden == y.Hidden && string.Equals(x.ColorRgb, y.ColorRgb);
            }

            public int GetHashCode(EditorLayerInfo obj)
            {
                unchecked
                {
                    var hashCode = obj.Name.GetHashCode();
                    hashCode = (hashCode * 397) ^ obj.Hidden.GetHashCode();
                    hashCode = (hashCode * 397) ^ obj.ColorRgb.GetHashCode();
                    return hashCode;
                }
            }
        }

        public static IEqualityComparer<EditorLayerInfo> ByValueComparer { get; } = new ByValueEqualityComparer();
    }
}
