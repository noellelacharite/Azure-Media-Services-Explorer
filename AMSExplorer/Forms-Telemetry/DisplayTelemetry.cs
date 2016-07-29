﻿//----------------------------------------------------------------------------------------------
//    Copyright 2016 Microsoft Corporation
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//---------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.IO;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Blob.Protocol;
using System.Web;
using System.Net;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;


namespace AMSExplorer
{
    public partial class DisplayTelemetry : Form
    {
        private CloudMediaContext _context;
        private Mainform MyMainForm;
        private CredentialsEntry _credentials;

        private object _entity;
        private bool _firsttime = true;
        private DateTime? _timerangeEnd;
        private DateTime _timerangeStart = DateTime.UtcNow.AddHours(-5);
        private string _storagePassword = "";
        private bool boolSavedStoragePassword = false;

        public DisplayTelemetry(Mainform mainform, object entity, CloudMediaContext context, CredentialsEntry credentials)
        {
            InitializeComponent();
            this.Icon = Bitmaps.Azure_Explorer_ico;
            MyMainForm = mainform;
            _context = context;
            _credentials = credentials;
            _entity = entity;
        }

        private void contextMenuStripDG_MouseClick(object sender, MouseEventArgs e)
        {
            ContextMenuStrip contextmenu = (ContextMenuStrip)sender;
            DataGridView DG = (DataGridView)contextmenu.SourceControl;

            if (DG.SelectedCells.Count == 1)
            {
                if (DG.SelectedCells[0].Value != null)
                {
                    System.Windows.Forms.Clipboard.SetText(DG.SelectedCells[0].Value.ToString());
                }
                else
                {
                    System.Windows.Forms.Clipboard.Clear();
                }
            }
        }


        private void DisplayTelemetry_Load(object sender, EventArgs e)
        {
            moreinfoLiveEncodingProfilelink.Links.Add(new LinkLabel.Link(0, moreinfoLiveEncodingProfilelink.Text.Length, Constants.LinkMoreInfoLiveEncoding));

            var monitorconfig = _context.MonitoringConfigurations.FirstOrDefault();
            if (monitorconfig == null)
            {
                this.Close();
                return;
            }

            var currentConfig = _context.NotificationEndPoints.Where(n => n.Id == monitorconfig.NotificationEndPointId).FirstOrDefault();
            if (currentConfig == null)
            {
                this.Close();
                return;
            }

            var storagename = (new Uri(currentConfig.EndPointAddress)).Host.Split(".".ToCharArray()).FirstOrDefault(); ;

            if (_context.DefaultStorageAccount.Name == storagename && !string.IsNullOrWhiteSpace(_credentials.StorageKey))
            {
                _storagePassword = _credentials.StorageKey;
            }
            else
            { // Default storage, no blob credentials, or another storage. Let's ask the user
                string valuekey = "";
                if (Program.InputBox("Storage Account Key Needed", "Please enter the Storage Account Access Key for " + storagename + ":", ref valuekey, true) == DialogResult.OK)
                {
                    if (_context.DefaultStorageAccount.Name == storagename)
                    {
                        _credentials.StorageKey = valuekey;
                        _storagePassword = valuekey;
                        boolSavedStoragePassword = true;
                    }
                }
                else
                {
                    this.Close();
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(_credentials.AccountId))
            { // No blob credentials. Let's ask the user
                string mediaServicesAccountID = "";
                if (Program.InputBox("AMS Account Id Needed", "Please enter the AMS Account ID for " + _credentials.AccountName + ":", ref mediaServicesAccountID, true) == DialogResult.OK)
                {
                    _credentials.AccountId = mediaServicesAccountID;
                }
                else
                {
                    this.Close();
                    return;
                }

            }

        }


        private void contextMenuStripDG_Opening(object sender, CancelEventArgs e)
        {

        }


        private void ChannelAdSlateControl_FormClosed(object sender, FormClosedEventArgs e)
        {

        }


        private void toolStripMenuItemFilesCopyClipboard_Click(object sender, EventArgs e)
        {

        }

        private void DisplayTelemetry_Shown(object sender, EventArgs e)
        {
            DoLoadTelemetry(_entity);


        }

        private void buttonDisregard_Click(object sender, EventArgs e)
        {
            this.Close();
        }


        private void moreinfoLiveEncodingProfilelink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            // Send the URL to the operating system.
            Process.Start(e.Link.LinkData as string);
        }

        private void DoLoadTelemetry(object myobject)
        {
            this.Cursor = Cursors.WaitCursor;
            if (_entity is IStreamingEndpoint)
            {
                DoLoadTelemetry((IStreamingEndpoint)_entity);
            }
            else if (_entity is IChannel)
            {
                DoLoadTelemetry((IChannel)_entity);
            }
            this.Cursor = Cursors.Default;
        }


        private void DoLoadTelemetry(IStreamingEndpoint streamingEndpoint)
        {
            if (_firsttime)
            {
                dataGridViewTelemetry.ColumnCount = 9;

                dataGridViewTelemetry.Columns[0].HeaderText = "ObservedTime (local)";
                dataGridViewTelemetry.Columns[1].HeaderText = "BytesSent";
                dataGridViewTelemetry.Columns[2].HeaderText = "EndToEndLatency";
                dataGridViewTelemetry.Columns[3].HeaderText = "HostName";
                dataGridViewTelemetry.Columns[4].HeaderText = "RequestCount";
                dataGridViewTelemetry.Columns[5].HeaderText = "ResultCode";
                dataGridViewTelemetry.Columns[6].HeaderText = "RowKey";
                dataGridViewTelemetry.Columns[7].HeaderText = "ServerLatency";
                dataGridViewTelemetry.Columns[8].HeaderText = "StatusCode";

                labelTelemetryUI.Text = string.Format("Telemetry for Streaming Endpoint '{0}'", streamingEndpoint.Name);

                _firsttime = false;
            }
            // Processors tab
            /*
            dataGridViewTelemetry.ColumnCount = 5;
            dataGridViewProcessors.Columns[0].HeaderText = "Vendor";
            dataGridViewProcessors.Columns[0].Width = 82;
            dataGridViewProcessors.Columns[1].HeaderText = "Name";
            dataGridViewProcessors.Columns[1].Width = 222;
            dataGridViewProcessors.Columns[2].HeaderText = "Version";
            dataGridViewProcessors.Columns[2].Width = 65;
            dataGridViewProcessors.Columns[3].HeaderText = "Id";
            dataGridViewProcessors.Columns[3].Width = 230;
            dataGridViewProcessors.Columns[4].HeaderText = "Description";
            dataGridViewProcessors.Columns[4].Width = 390;
            */
            dataGridViewTelemetry.Rows.Clear();

            var monitorconfig = _context.MonitoringConfigurations.FirstOrDefault();
            if (monitorconfig == null) return;

            var currentConfig = _context.NotificationEndPoints.Where(n => n.Id == monitorconfig.NotificationEndPointId).FirstOrDefault();
            if (currentConfig == null) return;

            try
            {
                // Get some streaming endpoint metrics.
                var res = _context.StreamingEndPointRequestLogs.GetStreamingEndPointMetrics(
                        currentConfig.EndPointAddress,
                        _storagePassword,
                        new Guid(_credentials.AccountId).ToString(),
                        streamingEndpoint.Id.Substring(12),
                        _timerangeStart,
                         _timerangeEnd ?? DateTime.UtcNow);


                foreach (var log in res)
                {
                    dataGridViewTelemetry.Rows.Add(log.ObservedTime.ToLocalTime(), log.BytesSent, log.EndToEndLatency, log.HostName, log.RequestCount, log.ResultCode, log.RowKey, log.ServerLatency, log.StatusCode);
                }
            }


            catch (Exception ex)
            {
                MessageBox.Show("Error when accessing to telemetry.\n\n" + ex.Message);
                _storagePassword = "";
                _credentials.AccountId = "";
                if (boolSavedStoragePassword) _credentials.StorageKey = "";
            }
        }

        private void DoLoadTelemetry(IChannel channel)
        {
            if (_firsttime)
            {
                dataGridViewTelemetry.ColumnCount = 9;

                dataGridViewTelemetry.Columns[0].HeaderText = "Observed time (local)";
                dataGridViewTelemetry.Columns[1].HeaderText = "Track type";
                dataGridViewTelemetry.Columns[2].HeaderText = "track name";
                dataGridViewTelemetry.Columns[3].HeaderText = "Bitrate";
                dataGridViewTelemetry.Columns[4].HeaderText = "Incoming bitrate";
                dataGridViewTelemetry.Columns[5].HeaderText = "Overlap count";
                dataGridViewTelemetry.Columns[6].HeaderText = "Discontinuity count";
                dataGridViewTelemetry.Columns[7].HeaderText = "Last timestamp";
                dataGridViewTelemetry.Columns[8].HeaderText = "Custom attributes";

                labelTelemetryUI.Text = string.Format("Telemetry for channel '{0}'", channel.Name);

                _firsttime = false;
            }
            dataGridViewTelemetry.Rows.Clear();

            // Processors tab
            /*
            dataGridViewTelemetry.ColumnCount = 5;
            dataGridViewProcessors.Columns[0].HeaderText = "Vendor";
            dataGridViewProcessors.Columns[0].Width = 82;
            dataGridViewProcessors.Columns[1].HeaderText = "Name";
            dataGridViewProcessors.Columns[1].Width = 222;
            dataGridViewProcessors.Columns[2].HeaderText = "Version";
            dataGridViewProcessors.Columns[2].Width = 65;
            dataGridViewProcessors.Columns[3].HeaderText = "Id";
            dataGridViewProcessors.Columns[3].Width = 230;
            dataGridViewProcessors.Columns[4].HeaderText = "Description";
            dataGridViewProcessors.Columns[4].Width = 390;
            */


            var monitorconfig = _context.MonitoringConfigurations.FirstOrDefault();
            if (monitorconfig == null) return;

            var currentConfig = _context.NotificationEndPoints.Where(n => n.Id == monitorconfig.NotificationEndPointId).FirstOrDefault();
            if (currentConfig == null) return;

            // Get some channel metrics.
            try
            {
                var channelMetrics = _context.ChannelMetrics.GetChannelMetrics(
              currentConfig.EndPointAddress,
                   _storagePassword,
                   new Guid(_credentials.AccountId).ToString(),
                   channel.Id.Substring(13),
               _timerangeStart,
               _timerangeEnd ?? DateTime.UtcNow);


                foreach (var cHB in channelMetrics.OrderBy(x => x.ObservedTime))
                {
                    dataGridViewTelemetry.Rows.Add(cHB.ObservedTime.ToLocalTime(), cHB.TrackType, cHB.TrackName, cHB.Bitrate, cHB.IncomingBitrate, cHB.OverlapCount, cHB.DiscontinuityCount, cHB.LastTimestamp, cHB.CustomAttributes);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error when accessing to telemetry.\n\n" + ex.Message);
                _storagePassword = "";
                _credentials.AccountId = "";
                if (boolSavedStoragePassword) _credentials.StorageKey = "";
            }

        }

        private void button1_Click(object sender, EventArgs e)
        {
            var form = new TimeRangeSelection()
            {
                TimeRangeStartDate = _timerangeStart,
                TimeRangeEndDate = _timerangeEnd,
                LabelMain = "Time Range for telemetry"
            };

            if (form.ShowDialog() == DialogResult.OK)
            {
                _timerangeStart = form.TimeRangeStartDate;
                _timerangeEnd = form.TimeRangeEndDate;
                DoLoadTelemetry(_entity);
            }
        }

        private void buttonRefresh_Click(object sender, EventArgs e)
        {
            DoLoadTelemetry(_entity);

        }
    }
}
