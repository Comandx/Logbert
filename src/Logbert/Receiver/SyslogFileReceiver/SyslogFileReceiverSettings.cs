﻿#region Copyright © 2015 Couchcoding

// File:    SyslogFileReceiverSettings.cs
// Package: Logbert
// Project: Logbert
// 
// The MIT License (MIT)
// 
// Copyright (c) 2015 Couchcoding
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

#endregion

using System;
using System.Windows.Forms;

using Com.Couchcoding.Logbert.Interfaces;
using Com.Couchcoding.Logbert.Properties;
using System.IO;

using Com.Couchcoding.Logbert.Helper;

namespace Com.Couchcoding.Logbert.Receiver.SyslogFileReceiver
{
  /// <summary>
  /// Implements the <see cref="ILogSettingsCtrl"/> control for the Syslog file receiver.
  /// </summary>
  public partial class SyslogFileReceiverSettings : UserControl, ILogSettingsCtrl
  {
    #region Private Methods

    /// <summary>
    /// Handles the Click event of the browse for file <see cref="Button"/>.
    /// </summary>
    private void TxtLogFileButtonClick(object sender, EventArgs e)
    {
      using (OpenFileDialog ofd = new OpenFileDialog())
      {
        ofd.CheckFileExists  = true;
        ofd.CheckPathExists  = true;
        ofd.FileName         = txtLogFile.Text;
        ofd.Filter           = Resources.strSyslogFileReceiverFilePattern;
        ofd.RestoreDirectory = true;

        if (ofd.ShowDialog(this) == DialogResult.OK)
        {
          txtLogFile.Text = ofd.FileName;
        }
      }
    }

    /// <summary>
    /// Raises the <see cref="E:System.Windows.Forms.UserControl.Load"/> event.
    /// </summary>
    /// <param name="e">An <see cref="T:System.EventArgs"/> that contains the event data. </param>
    protected override void OnLoad(EventArgs e)
    {
      base.OnLoad(e);

      if (ModifierKeys != Keys.Shift)
      {
        if (!string.IsNullOrEmpty(Settings.Default.PnlSyslogFileSettingsFile)
        &&  File.Exists(Settings.Default.PnlSyslogFileSettingsFile))
        {
          txtLogFile.Text = Settings.Default.PnlSyslogFileSettingsFile;
        }

        chkStartFromBeginning.Checked = Settings.Default.PnlSyslogFileSettingsStartFromBeginning;
      }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Validates the entered settings.
    /// </summary>
    /// <returns>The <see cref="ValidationResult"/> of the validation.</returns>
    public ValidationResult ValidateSettings()
    {
      if (File.Exists(txtLogFile.Text))
      {
        return ValidationResult.Success;
      }

      txtLogFile.SelectAll();
      txtLogFile.Select();

      return ValidationResult.Error(Resources.strSyslogFileReceiverFileDoesNotExist);
    }

    /// <summary>
    /// Creates and returns a fully configured <see cref="ILogProvider"/> instance.
    /// </summary>
    /// <returns>A fully configured <see cref="ILogProvider"/> instance.</returns>
    public ILogProvider GetConfiguredInstance()
    {
      if (ModifierKeys != Keys.Shift)
      {
        // Save the current settings as new default values.
        Settings.Default.PnlSyslogFileSettingsFile               = txtLogFile.Text;
        Settings.Default.PnlSyslogFileSettingsStartFromBeginning = chkStartFromBeginning.Checked;

        Settings.Default.SaveSettings();
      }

      return new SyslogFileReceiver(
          txtLogFile.Text
        , chkStartFromBeginning.Checked);
    }

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new instance of the <see cref="SyslogFileReceiverSettings"/> <see cref="Control"/>.
    /// </summary>
    public SyslogFileReceiverSettings()
    {
      InitializeComponent();
    }

    #endregion
  }
}
