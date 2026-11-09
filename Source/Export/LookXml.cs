using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using UnityEngine;
using Verse;

namespace VisualProgression.AgePicker
{
    /// <summary>Writes a look's graphic data as the XML a pack would give it, so the game reads back the same values.</summary>
    public static class LookXml
    {
        private static readonly GraphicData Defaults = new GraphicData();

        private static readonly List<FieldInfo> GraphicFields = typeof(GraphicData)
            .GetFields(BindingFlags.Instance | BindingFlags.Public)
            .OrderBy(field => field.MetadataToken)
            .ToList();

        /// <summary>
        /// A graphicData element: the Class and fromMod of art from a chosen mod, then every field whose value differs
        /// from a new GraphicData's. Check <see cref="UnwritableField"/> first.
        /// </summary>
        public static XElement GraphicDataElement(GraphicData data)
        {
            var element = new XElement("graphicData");
            if (data is GraphicData_FromMod fromModData)
            {
                element.SetAttributeValue("Class", typeof(GraphicData_FromMod).FullName);
                if (!fromModData.fromMod.NullOrEmpty())
                {
                    element.Add(new XElement("fromMod", fromModData.fromMod));
                }
            }
            foreach (FieldInfo field in SetFields(data))
            {
                element.Add(new XElement(field.Name, TextOf(field.GetValue(data))));
            }
            return element;
        }

        /// <summary>
        /// The name of the first field set to a value written as nested XML, like shadows or attachments, which the export
        /// doesn't write, or null when there's none.
        /// </summary>
        public static string UnwritableField(GraphicData data)
        {
            return SetFields(data).FirstOrDefault(field => !IsText(field.GetValue(data)))?.Name;
        }

        /// <summary>A value as ParseHelper reads it: numbers in the invariant culture, vectors and colours in brackets.</summary>
        public static string TextOf(object value)
        {
            switch (value)
            {
                case float number:
                    return number.ToString(CultureInfo.InvariantCulture);
                case Vector2 vector:
                    return Bracketed(vector.x, vector.y);
                case Vector3 vector:
                    return Bracketed(vector.x, vector.y, vector.z);
                case Color colour:
                    return Bracketed(colour.r, colour.g, colour.b, colour.a);
                case Type type:
                    return GenTypes.GetTypeNameWithoutIgnoredNamespaces(type);
                case Def def:
                    return def.defName;
                case IFormattable formattable:
                    return formattable.ToString(null, CultureInfo.InvariantCulture);
                default:
                    return value.ToString();
            }
        }

        private static IEnumerable<FieldInfo> SetFields(GraphicData data)
        {
            return GraphicFields.Where(field =>
            {
                object value = field.GetValue(data);
                return value != null && !Equals(value, field.GetValue(Defaults));
            });
        }

        // Values ParseHelper reads from an element's text.
        private static bool IsText(object value)
        {
            return DirectXmlSaver.IsSimpleTextType(value.GetType()) || value is Vector2 || value is Vector3 ||
                value is Color || value is Type || value is Def;
        }

        private static string Bracketed(params float[] numbers)
        {
            return "(" + string.Join(", ", numbers.Select(number => number.ToString(CultureInfo.InvariantCulture))) + ")";
        }
    }
}
