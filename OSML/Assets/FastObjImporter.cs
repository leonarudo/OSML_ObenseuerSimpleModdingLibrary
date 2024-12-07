using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.IO;

namespace OSML.Assets
{
    /* Source:
     * 
     * TLDLoader
     * Author: KolbenLP
     * Version: 1.4.1.0
     */

    public class FastObjImporter
    {
        private static FastObjImporter _instance;
        private List<int> triangles;
        private List<Vector3> vertices;
        private List<Vector2> uv;
        private List<Vector3> normals;
        private List<Vector3Int> faceData;
        private List<int> intArray;
        private const int MIN_POW_10 = -16;
        private const int MAX_POW_10 = 16;
        private const int NUM_POWS_10 = 33;
        private static readonly float[] pow10 = FastObjImporter.GenerateLookupTable();

        public static FastObjImporter Instance => FastObjImporter._instance ?? (FastObjImporter._instance = new FastObjImporter());

        public Mesh ImportFile(string filePath)
        {
            this.triangles = new List<int>();
            this.vertices = new List<Vector3>();
            this.uv = new List<Vector2>();
            this.normals = new List<Vector3>();
            this.faceData = new List<Vector3Int>();
            this.intArray = new List<int>();
            this.LoadMeshData(filePath);
            Vector3[] vector3Array1 = new Vector3[this.faceData.Count];
            Vector2[] vector2Array = new Vector2[this.faceData.Count];
            Vector3[] vector3Array2 = new Vector3[this.faceData.Count];
            for (int index = 0; index < this.faceData.Count; ++index)
            {
                vector3Array1[index] = this.vertices[this.faceData[index].x - 1];
                if (this.faceData[index].y >= 1 && this.faceData[index].y - 1 < this.uv.Count - 1)
                    vector2Array[index] = this.uv[this.faceData[index].y - 1];
                if (this.faceData[index].z >= 1)
                    vector3Array2[index] = this.normals[this.faceData[index].z - 1];
            }
            Mesh mesh = new Mesh();
            mesh.vertices = vector3Array1;
            mesh.uv = vector2Array;
            mesh.normals = vector3Array2;
            mesh.triangles = this.triangles.ToArray();
            mesh.RecalculateBounds();
            mesh.Optimize();
            return mesh;
        }

        private void LoadMeshData(string fileName)
        {
            StringBuilder sb = new StringBuilder();
            string str1 = File.ReadAllText(fileName);
            int num1 = 0;
            string str2 = (string)null;
            int num2 = 0;
            StringBuilder stringBuilder = new StringBuilder();
            for (int index1 = 0; index1 < str1.Length; ++index1)
            {
                if (str1[index1] == '\n')
                {
                    sb.Remove(0, sb.Length);
                    sb.Append(str1, num1 + 1, index1 - num1);
                    num1 = index1;
                    if (sb[0] == 'o' && sb[1] == ' ')
                    {
                        stringBuilder.Remove(0, stringBuilder.Length);
                        for (int index2 = 2; index2 < sb.Length; ++index2)
                            str2 += sb[index2].ToString();
                    }
                    else if (sb[0] == 'v' && sb[1] == ' ')
                    {
                        int start = 2;
                        this.vertices.Add(new Vector3(this.GetFloat(sb, ref start, ref stringBuilder), this.GetFloat(sb, ref start, ref stringBuilder), this.GetFloat(sb, ref start, ref stringBuilder)));
                    }
                    else if (sb[0] == 'v' && sb[1] == 't' && sb[2] == ' ')
                    {
                        int start = 3;
                        this.uv.Add(new Vector2(this.GetFloat(sb, ref start, ref stringBuilder), this.GetFloat(sb, ref start, ref stringBuilder)));
                    }
                    else if (sb[0] == 'v' && sb[1] == 'n' && sb[2] == ' ')
                    {
                        int start = 3;
                        this.normals.Add(new Vector3(this.GetFloat(sb, ref start, ref stringBuilder), this.GetFloat(sb, ref start, ref stringBuilder), this.GetFloat(sb, ref start, ref stringBuilder)));
                    }
                    else if (sb[0] == 'f' && sb[1] == ' ')
                    {
                        int start = 2;
                        int num3 = 1;
                        this.intArray.Clear();
                        int num4 = 0;
                        while (start < sb.Length && char.IsDigit(sb[start]))
                        {
                            this.faceData.Add(new Vector3Int(this.GetInt(sb, ref start, ref stringBuilder), this.GetInt(sb, ref start, ref stringBuilder), this.GetInt(sb, ref start, ref stringBuilder)));
                            ++num3;
                            this.intArray.Add(num2);
                            ++num2;
                        }
                        int num5 = num4 + num3;
                        for (int index3 = 1; index3 + 2 < num5; ++index3)
                        {
                            this.triangles.Add(this.intArray[0]);
                            this.triangles.Add(this.intArray[index3]);
                            this.triangles.Add(this.intArray[index3 + 1]);
                        }
                    }
                }
            }
        }

        private float GetFloat(StringBuilder sb, ref int start, ref StringBuilder sbFloat)
        {
            sbFloat.Remove(0, sbFloat.Length);
            while (start < sb.Length && (char.IsDigit(sb[start]) || sb[start] == '-' || sb[start] == '.'))
            {
                sbFloat.Append(sb[start]);
                ++start;
            }
            ++start;
            return this.ParseFloat(sbFloat);
        }

        private int GetInt(StringBuilder sb, ref int start, ref StringBuilder sbInt)
        {
            sbInt.Remove(0, sbInt.Length);
            while (start < sb.Length && char.IsDigit(sb[start]))
            {
                sbInt.Append(sb[start]);
                ++start;
            }
            ++start;
            return this.IntParseFast(sbInt);
        }

        private static float[] GenerateLookupTable()
        {
            float[] lookupTable = new float[320];
            for (int index = 0; index < lookupTable.Length; ++index)
                lookupTable[index] = (float)(index / 33) * Mathf.Pow(10f, (float)(index % 33 - 16));
            return lookupTable;
        }

        private float ParseFloat(StringBuilder value)
        {
            float num1 = 0.0f;
            bool flag = false;
            int length = value.Length;
            int num2 = value.Length;
            for (int index = length - 1; index >= 0; --index)
            {
                if (value[index] == '.')
                {
                    num2 = index;
                    break;
                }
            }
            int num3 = 16 + num2;
            for (int index = 0; index < num2; ++index)
            {
                if (index != num2 && value[index] != '-')
                    num1 += FastObjImporter.pow10[((int)value[index] - 48) * 33 + num3 - index - 1];
                else if (value[index] == '-')
                    flag = true;
            }
            for (int index = num2 + 1; index < length; ++index)
            {
                if (index != num2)
                    num1 += FastObjImporter.pow10[((int)value[index] - 48) * 33 + num3 - index];
            }
            if (flag)
                num1 = -num1;
            return num1;
        }

        private int IntParseFast(StringBuilder value)
        {
            int fast = 0;
            for (int index = 0; index < value.Length; ++index)
                fast = 10 * fast + ((int)value[index] - 48);
            return fast;
        }
    }
}
