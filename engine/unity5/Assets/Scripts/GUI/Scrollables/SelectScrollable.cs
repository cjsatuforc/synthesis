﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Synthesis.GUI.Scrollables
{
    public class SelectScrollable : ScrollablePanel
    {
        public string TargetFilename;
        public string ErrorMessage;

        /// <summary>
        /// Refreshes the scrollable with the directory provided.
        /// </summary>
        /// <param name="directory"></param>
        public void Refresh(string directory)
        {
            string[] folders = Directory.GetDirectories(directory);

            items.Clear();

            foreach (string robot in folders)
                if (File.Exists(robot + "\\" + TargetFilename))
                    items.Add(new DirectoryInfo(robot).Name);

            if (items.Count > 0)
                selectedEntry = items[0];

            position = UnityEngine.Camera.main.WorldToScreenPoint(transform.position);
        }

        /// <summary>
        /// Clears the list of items when this <see cref="SelectScrollable"/>
        /// is enabled.
        /// </summary>
        void OnEnable()
        {
            errorMessage = ErrorMessage;

            items = new List<string>();
            items.Clear();
        }
    }
}
