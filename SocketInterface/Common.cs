using System;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace myJSON
{

    public class FileItem
    {
        public string fileFullName { get; set; }
        public string fileName { get; set; }
        public int fileSize_Bytes { get; set; }
    }

    public static class DataTypes
    {
        public static string String = "string";
        public static string File = "file";
    }

    public static class JsonItems
    {
        public static string DataType = "DataType";
        public static string StringLength = "StringLength";
        public static string FileName = "FileName";
        public static string FileSize = "FileSize";
        public static string DataLength = "DataLength";
    }

    public class toolJSON
    {
        public string FileInfo(List<FileItem> fileItems, int totalByteLength)
        {
            JObject tempJSON = new JObject();
            tempJSON.Add(JsonItems.DataType, DataTypes.File);

            string fileNames = "";
            string fileSizes = "";
            bool firstItem = true;
            foreach (FileItem item in fileItems)
            {
                if (firstItem)
                {
                    firstItem = false;
                }
                else
                {
                    fileNames += ",";
                    fileSizes += ",";
                }
                fileNames += item.fileName;
                fileSizes += item.fileSize_Bytes.ToString();
            }
            tempJSON.Add(JsonItems.FileName, fileNames);
            tempJSON.Add(JsonItems.FileSize, fileSizes);
            tempJSON.Add(JsonItems.DataLength, totalByteLength.ToString());

            return JsonConvert.SerializeObject(tempJSON);
        }
        public string FileInfo(string fileName, int fileSize, int totalByteLength)
        {
            JObject tempJSON = new JObject();
            tempJSON.Add(JsonItems.DataType, DataTypes.File);
            tempJSON.Add(JsonItems.FileName, fileName);
            tempJSON.Add(JsonItems.FileSize, fileSize.ToString());
            tempJSON.Add(JsonItems.DataLength, totalByteLength.ToString());

            return JsonConvert.SerializeObject(tempJSON);
        }
        public string StringInfo(int stringDataLength, int dataTotalLength)
        {
            JObject tempJSON = new JObject();
            tempJSON.Add(JsonItems.DataType, DataTypes.String);
            tempJSON.Add(JsonItems.StringLength, stringDataLength.ToString());
            tempJSON.Add(JsonItems.DataLength, dataTotalLength.ToString());

            return JsonConvert.SerializeObject(tempJSON);
        }

        public string GetData_FromJSON(string str, string item)
        {
            string result = "";

            try
            {
                JObject tempJSON = JObject.Parse(str);
                result = tempJSON[item].ToString();
            }
            catch (Exception ex)
            {
                return ("예외 발생 : " + ex.ToString());
            }

            return result;
        }


    }
}
