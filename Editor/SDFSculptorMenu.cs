using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

namespace SDFSculptor
{
    public class SDFSculptorMenu : MonoBehaviour
    {
        [MenuItem("Tools/SDFSculptor/Convert SDFSculptor Materials In Scene To URP")]
        static void ConvertToURP()
        {
            ConvertRP(true, false);
        }

        [MenuItem("Tools/SDFSculptor/Convert SDFSculptor Materials In Scene To HDRP")]
        static void ConvertToHDRP()
        {
            ConvertRP(false, true);
        }

        static void ConvertRP(bool urp, bool hdrp)
        {
            List<Material> URPMats = new List<Material>();
            List<Material> HDRPMats = new List<Material>();

            string relativepath = "";
            var info = new DirectoryInfo("Assets/SDFSculptor/Materials/URP");
            FileInfo[] fileInfo = info.GetFiles("*.mat", SearchOption.AllDirectories);
            foreach (FileInfo file in fileInfo)
            {
                relativepath = "Assets" + file.FullName.Substring(Application.dataPath.Length);
                Material prefab = (Material)AssetDatabase.LoadAssetAtPath(relativepath, typeof(Material));
                URPMats.Add(prefab);
            }

            info = new DirectoryInfo("Assets/SDFSculptor/Materials/HDRP");
            fileInfo = info.GetFiles("*.mat", SearchOption.AllDirectories);
            foreach (FileInfo file in fileInfo)
            {
                relativepath = "Assets" + file.FullName.Substring(Application.dataPath.Length);
                Material prefab = (Material)AssetDatabase.LoadAssetAtPath(relativepath, typeof(Material));
                HDRPMats.Add(prefab);
            }

            MeshRenderer[] allRenderers = GameObject.FindObjectsOfType<MeshRenderer>();

            if(urp)
            {
                foreach(MeshRenderer renderer in allRenderers)
                {
                    if(HDRPMats.Contains(renderer.sharedMaterial))
                    {
                        renderer.sharedMaterial = ReplaceMat(renderer.sharedMaterial, URPMats);
                    }
                }
            }
            else if(hdrp)
            {
                foreach(MeshRenderer renderer in allRenderers)
                {
                    if(URPMats.Contains(renderer.sharedMaterial))
                    {
                        renderer.sharedMaterial = ReplaceMat(renderer.sharedMaterial, HDRPMats);
                    }
                }
            }
        }

        static Material ReplaceMat (Material curMat, List<Material> matList)
        {
            Debug.Log(curMat.name);
            for (int i = 0; i < matList.Count; i++)
            {
                if(matList[i].name == curMat.name)
                {
                    return matList[i];
                }
            }
            return curMat;
        }
    }
}
