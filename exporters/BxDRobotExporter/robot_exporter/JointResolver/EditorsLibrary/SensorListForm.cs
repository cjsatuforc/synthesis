﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EditorsLibrary
{
    public partial class SensorListForm : Form
    {
        SkeletalJoint_Base joint;

        public SensorListForm(SkeletalJoint_Base passJoint)
        {
            InitializeComponent();

            joint = passJoint;
            this.UpdateSensorList();
            
            base.Location = new System.Drawing.Point(Cursor.Position.X - 10, Cursor.Position.Y - base.Height - 10);
        }

        private void UpdateSensorList()
        {
            sensorListView.Items.Clear();

            foreach (RobotSensor sensor in joint.attachedSensors)
            {
                if (sensor.type.Equals(RobotSensorType.ENCODER))
                {// if the sensor is an encoder show both ports
                    System.Windows.Forms.ListViewItem item = new System.Windows.Forms.ListViewItem(new string[] {
                    char.ToUpper(sensor.type.ToString()[0]) + sensor.type.ToString().Substring(1).ToLower(),
                        sensor.portA.ToString(), sensor.portB.ToString()});
                    item.Tag = sensor;
                    sensorListView.Items.Add(item);
                } else
                {
                    System.Windows.Forms.ListViewItem item = new System.Windows.Forms.ListViewItem(new string[] {
                    char.ToUpper(sensor.type.ToString()[0]) + sensor.type.ToString().Substring(1).ToLower(),
                        sensor.portA.ToString()});
                    item.Tag = sensor;
                    sensorListView.Items.Add(item);

                }
            }
        }

        /// <summary>
        /// Removes the selected sensor from the list of sensors in the joint.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void deleteButton_Click(object sender, EventArgs e)
        {
            if (sensorListView.SelectedItems.Count > 0 && sensorListView.SelectedItems[0].Tag is RobotSensor)
            {
                joint.attachedSensors.Remove((RobotSensor) sensorListView.SelectedItems[0].Tag);
                this.UpdateSensorList();
                LegacyInterchange.LegacyEvents.OnRobotModified();
            }
        }

        private void addSensorButton_Click(object sender, EventArgs e)
        {
            EditSensorForm sensorForm = new EditSensorForm(joint, joint.attachedSensors.IndexOf(
                sensorListView.SelectedItems.Count > 0 &&
                sensorListView.SelectedItems[0].Tag is RobotSensor ?
                (RobotSensor) sensorListView.SelectedItems[0].Tag : null));
            sensorForm.ShowDialog(this);
            this.UpdateSensorList();
        }

        private void window_SizeChanged(object sender, EventArgs e)
        {
            int newListHeight = this.Height - 115;
            int newListWidth = this.Width - 45;

            sensorListView.Height = newListHeight;
            sensorListView.Width = newListWidth;
        }
        private void sensorListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            addSensorButton.Text = joint.attachedSensors.IndexOf(
                sensorListView.SelectedItems.Count > 0 &&
                sensorListView.SelectedItems[0].Tag is RobotSensor ?
                (RobotSensor) sensorListView.SelectedItems[0].Tag : null) >= 0 ? "Edit Sensor" : "Add Sensor";
        }
        private void closeButton_Click(object sender, EventArgs e)
        {
            Close();
        }
        private void sensorListView_ColumnWidthChanging(object sender, ColumnWidthChangingEventArgs e)
        {
            e.Cancel = true;
            e.NewWidth = this.sensorListView.Columns[e.ColumnIndex].Width;
        }
        private void sensorListView_SelectedIndexChanged(object sender, MouseEventArgs e)
        {
            addSensorButton.Text = joint.attachedSensors.IndexOf(
                sensorListView.SelectedItems.Count > 0 &&
                sensorListView.SelectedItems[0].Tag is RobotSensor ?
                (RobotSensor)sensorListView.SelectedItems[0].Tag : null) >= 0 ? "Edit Sensor" : "Add Sensor";
        }
        
    }
}
