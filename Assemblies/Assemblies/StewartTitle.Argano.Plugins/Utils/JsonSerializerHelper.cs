// <copyright file="JsonSerializerHelper.cs" company="Argano">
// Copyright 2023 Argano
// </copyright>

namespace StewartTitle.Argano.Plugins.Utils
{
    using System.IO;
    using System.Runtime.Serialization.Json;
    using System.Text;

    /// <summary>
    /// Helper methods to provide functionality similar to System.Text.Json.JsonSerializer for .NET Framework.
    /// </summary>
    public static class JsonSerializerHelper
    {
        /// <summary>
        /// Serializes an object into JSON.
        /// </summary>
        /// <typeparam name="T">Type for serialization/deserealization.</typeparam>
        /// <param name="t">Object to serialize.</param>
        /// <returns>JSON string.</returns>
        public static string JsonSerializer<T>(T t)
        {
            DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(T));
            MemoryStream ms = new MemoryStream();
            ser.WriteObject(ms, t);
            string jsonString = Encoding.UTF8.GetString(ms.ToArray());
            ms.Close();
            return jsonString;
        }

        /// <summary>
        /// Deserializes an object from JSON.
        /// </summary>
        /// <typeparam name="T">Type for serialization/deserealization.</typeparam>
        /// <param name="jsonString">JSON string to deserialize.</param>
        /// <returns>Object instance of type T, obtained from JSON string.</returns>
        public static T JsonDeserialize<T>(string jsonString)
        {
            DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(T));
            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(jsonString));
            T obj = (T)ser.ReadObject(ms);
            return obj;
        }
    }
}
