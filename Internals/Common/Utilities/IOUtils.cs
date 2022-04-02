﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TanksRebirth.GameContent;

namespace TanksRebirth.Internals.Common.Utilities
{
    public static class IOUtils
    {
        public static string[] GetSubFolders(string root, bool getName = false)
        {
            var rootGotten = Directory.GetDirectories(root);

            string[] names = new string[rootGotten.Length];

            int curTotal = 0;
            if (getName)
            {
                foreach (var dir in rootGotten)
                {
                    var dirInfo = new DirectoryInfo(dir);

                    names[curTotal] = dirInfo.Name;
                    curTotal++;
                }
            }
            return !getName ? rootGotten : names;
        }
        /*public static void CopyDir(string sourcePath, string targetPath, bool deleteOld)
        {
            //Now Create all of the directories
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
            }

            //Copy all the files & Replaces any files with the same name
            foreach (string fileName in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(fileName, fileName.Replace(sourcePath, targetPath), true);
                if (deleteOld)
                    File.Delete(fileName);
            }
        }*/
        // copy a folder's contents and paste them into another folder
        public static void CopyFolder(string sourceFolder, string destFolder, bool deleteOld)
        {
            if (!Directory.Exists(destFolder))
                Directory.CreateDirectory(destFolder);

            string[] files = Directory.GetFiles(sourceFolder);
            string[] folders = Directory.GetDirectories(sourceFolder);

            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                string dest = Path.Combine(destFolder, name);
                File.Copy(file, dest, true);
                if (deleteOld)
                    File.Delete(file);
            }

            foreach (string folder in folders)
            {
                string name = Path.GetFileName(folder);
                string dest = Path.Combine(destFolder, name);
                CopyFolder(folder, dest, deleteOld);
            }
        }
    }
}
