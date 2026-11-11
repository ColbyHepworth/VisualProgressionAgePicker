using System;
using System.Collections;
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
        /// A graphicData element: the Class, fromMod and skipMods of art from a chosen mod, then every field whose value differs
        /// from a new GraphicData's. Settings made of several values, like a building's shadow and damage areas, are
        /// written as nested elements, as vanilla's furniture styles write them. Check <see cref="UnwritableField"/> first.
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
                if (!fromModData.skipMods.NullOrEmpty())
                {
                    element.Add(new XElement("skipMods", fromModData.skipMods.Select(packageId => new XElement("li", packageId))));
                }
            }
            foreach (FieldInfo field in SetFields(data))
            {
                element.Add(Element(field.Name, field.GetValue(data)));
            }
            return element;
        }

        /// <summary>
        /// The name of the first field set to a value the export can't write, like attachments, or null when there's none.
        /// </summary>
        public static string UnwritableField(GraphicData data)
        {
            return SetFields(data).FirstOrDefault(field => !IsWritable(field.GetValue(data)))?.Name;
        }

        private static XElement Element(string name, object value)
        {
            if (IsText(value))
            {
                return new XElement(name, TextOf(value));
            }
            if (value is List<string> texts)
            {
                return new XElement(name, texts.Select(text => new XElement("li", text)));
            }
            return new XElement(name, NestedSetFields(value).Select(field => Element(field.Name, field.GetValue(value))));
        }

        private static bool IsWritable(object value)
        {
            return IsText(value) || value is List<string> || IsNested(value);
        }

        // An object of plain settings, like ShadowData or DamageGraphicData, whose set fields can all be written.
        private static bool IsNested(object value)
        {
            Type type = value.GetType();
            return !(value is IEnumerable) && type.IsClass && type.GetConstructor(Type.EmptyTypes) != null &&
                NestedSetFields(value).All(field => IsWritable(field.GetValue(value)));
        }

        // A nested object's public fields that differ from a new object's, leaving out the materials the game makes from
        // them while it loads.
        private static IEnumerable<FieldInfo> NestedSetFields(object value)
        {
            object defaults = Activator.CreateInstance(value.GetType());
            return value.GetType()
                .GetFields(BindingFlags.Instance | BindingFlags.Public)
                .OrderBy(field => field.MetadataToken)
                .Where(field => !IsMadeWhileLoading(field.FieldType))
                .Where(field =>
                {
                    object fieldValue = field.GetValue(value);
                    return fieldValue != null && !Equals(fieldValue, field.GetValue(defaults));
                });
        }

        private static bool IsMadeWhileLoading(Type type)
        {
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                return true;
            }
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>) &&
                typeof(UnityEngine.Object).IsAssignableFrom(type.GetGenericArguments()[0]);
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
                case Rect rect:
                    return Bracketed(rect.x, rect.y, rect.width, rect.height);
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
                value is Color || value is Rect || value is Type || value is Def;
        }

        private static string Bracketed(params float[] numbers)
        {
            return "(" + string.Join(", ", numbers.Select(number => number.ToString(CultureInfo.InvariantCulture))) + ")";
        }
    }
}
