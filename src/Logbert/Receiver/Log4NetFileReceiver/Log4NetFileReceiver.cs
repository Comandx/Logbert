﻿#region Copyright © 2015 Couchcoding

// File:    Log4NetFileReceiver.cs
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
using System.Collections.Generic;
using System.IO;
using Com.Couchcoding.Logbert.Controls;
using Com.Couchcoding.Logbert.Helper;
using Com.Couchcoding.Logbert.Interfaces;
using Com.Couchcoding.Logbert.Logging;

namespace Com.Couchcoding.Logbert.Receiver.Log4NetFileReceiver
{
    /// <summary>
    /// Implements a <see cref="ILogProvider"/> for the Log4Net file service.
    /// </summary>
    public class Log4NetFileReceiver : ReceiverBase
    {
        #region Private Consts

        /// <summary>
        /// Defines the end tag of a Log4Net message.
        /// </summary>
        private const string LOG4NET_LOGMSG_END = "</log4j:event>";

        #endregion

        #region Private Fields

        /// <summary>
        /// Holds the name of the File to observe.
        /// </summary>
        private readonly string mFileToObserve;

        /// <summary>
        /// Determines whether the file to observed should be read from beginning, or not.
        /// </summary>
        private readonly bool mStartFromBeginning;

        /// <summary>
        /// The <see cref="FileSystemWatcher"/> used to observe file content changes.
        /// </summary>
        private FileSystemWatcher mFileWatcher;

        /// <summary>
        /// The <see cref="StreamReader"/> used to read the log file content.
        /// </summary>
        private StreamReader mFileReader;

        /// <summary>
        /// Holds the offset of the last read line within the log file.
        /// </summary>
        private long mLastFileOffset;

        /// <summary>
        /// Counts the received messages;
        /// </summary>
        private int mLogNumber;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the name of the <see cref="ILogProvider"/>.
        /// </summary>
        public override string Name
        {
            get {
                return "Log4Net File Receiver";
            }
        }

        /// <summary>
        /// Gets the description of the <see cref="ILogProvider"/>
        /// </summary>
        public override string Description
        {
            get {
                return string.Format(
                    "{0} ({1})"
                  , Name
                  , !string.IsNullOrEmpty( mFileToObserve ) ? Path.GetFileName( mFileToObserve ) : "-" );
            }
        }

        /// <summary>
        /// Gets the filename for export of the received <see cref="LogMessage"/>s.
        /// </summary>
        public override string ExportFileName
        {
            get {
                return Description;
            }
        }

        /// <summary>
        /// Gets the tooltip to display at the document tab.
        /// </summary>
        public override string Tooltip
        {
            get {
                return mFileToObserve;
            }
        }

        /// <summary>
        /// Gets the settings <see cref="Control"/> of the <see cref="ILogProvider"/>.
        /// </summary>
        public override ILogSettingsCtrl Settings
        {
            get {
                return new Log4NetFileReceiverSettings();
            }
        }

        /// <summary>
        /// Gets the columns to display of the <see cref="ILogProvider"/>.
        /// </summary>
        public override Dictionary<int, string> Columns
        {
            get {
                return new Dictionary<int, string>
                {
          { 0, "Number"    },
          { 1, "Level"     },
          { 2, "Timestamp" },
          { 3, "Logger"    },
          { 4, "Thread"    },
          { 5, "Message"   },
        };
            }
        }

        /// <summary>
        /// Gets or sets the active state if the <see cref="ILogProvider"/>.
        /// </summary>
        public override bool IsActive
        {
            get {
                return base.IsActive;
            }
            set {
                base.IsActive = value;

                if( mFileWatcher != null ) {
                    // Update the observer state.
                    mFileWatcher.EnableRaisingEvents = base.IsActive;
                }
            }
        }

        /// <summary>
        /// Get the <see cref="Control"/> to display details about a selected <see cref="LogMessage"/>.
        /// </summary>
        public override ILogPresenter DetailsControl
        {
            get {
                return new Log4NetDetailsControl();
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Handles the FileChanged event of the <see cref="FileSystemWatcher"/> instance.
        /// </summary>
        private void OnLogFileChanged( object sender, FileSystemEventArgs e )
        {
            if( e.ChangeType == WatcherChangeTypes.Changed ) {
                ReadNewLogMessagesFromFile();
            }
        }

        /// <summary>
        /// Handles the Error event of the <see cref="FileSystemWatcher"/>.
        /// </summary>
        private void OnFileWatcherError( object sender, ErrorEventArgs e )
        {
            // Stop further listening on error.
            if( mFileWatcher != null ) {
                mFileWatcher.EnableRaisingEvents = false;
                mFileWatcher.Changed -= OnLogFileChanged;
                mFileWatcher.Error -= OnFileWatcherError;
                mFileWatcher.Dispose();
            }

            string pathOfFile = Path.GetDirectoryName( mFileToObserve );
            string nameOfFile = Path.GetFileName( mFileToObserve );

            if( !string.IsNullOrEmpty( pathOfFile ) && !string.IsNullOrEmpty( nameOfFile ) ) {
                mFileWatcher = new FileSystemWatcher(
                    pathOfFile
                  , nameOfFile );

                mFileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
                mFileWatcher.Changed += OnLogFileChanged;
                mFileWatcher.Error += OnFileWatcherError;
                mFileWatcher.EnableRaisingEvents = IsActive;

                ReadNewLogMessagesFromFile();
            }
        }

        /// <summary>
        /// Reads possible new log file entries form the file that is observed.
        /// </summary>
        private void ReadNewLogMessagesFromFile()
        {
            if( mFileReader == null || Equals( mFileReader.BaseStream.Length, mLastFileOffset ) ) {
                return;
            }

            mFileReader.BaseStream.Seek(
                mLastFileOffset
              , SeekOrigin.Begin );

            string line;
            string dataToParse = string.Empty;

            List<LogMessage> messages = new List<LogMessage>();

            while( ( line = mFileReader.ReadLine() ) != null ) {
                dataToParse += line;

                int log4NetEndTag = dataToParse.IndexOf(
                    LOG4NET_LOGMSG_END
                  , StringComparison.Ordinal );

                if( log4NetEndTag > 0 ) {
                    LogMessage newLogMsg;

                    try {
                        newLogMsg = new LogMessageLog4Net(
                            dataToParse
                          , ++mLogNumber );
                    }
                    catch( Exception ex ) {
                        Logger.Warn( ex.Message );
                        continue;
                    }

                    messages.Add( newLogMsg );

                    dataToParse = dataToParse.Substring(
                        log4NetEndTag
                      , dataToParse.Length - ( log4NetEndTag + LOG4NET_LOGMSG_END.Length ) );
                }
            }

            mLastFileOffset = mFileReader.BaseStream.Position;

            mLogHandler?.HandleMessage( messages.ToArray() );
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            return Name;
        }

        /// <summary>
        /// Intizializes the <see cref="ILogProvider"/>.
        /// </summary>
        /// <param name="logHandler">The <see cref="ILogHandler"/> that may handle incomming <see cref="LogMessage"/>s.</param>
        public override void Initialize( ILogHandler logHandler )
        {
            base.Initialize( logHandler );

            mFileReader = new StreamReader( new FileStream(
                mFileToObserve
              , FileMode.Open
              , FileAccess.Read
              , FileShare.ReadWrite ) );

            mLogNumber = 0;
            mLastFileOffset = mStartFromBeginning
              ? 0
              : mFileReader.BaseStream.Length;

            string pathOfFile = Path.GetDirectoryName( mFileToObserve );
            string nameOfFile = Path.GetFileName( mFileToObserve );

            if( !string.IsNullOrEmpty( pathOfFile ) && !string.IsNullOrEmpty( nameOfFile ) ) {
                mFileWatcher = new FileSystemWatcher(
                    pathOfFile
                  , nameOfFile );

                mFileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
                mFileWatcher.Changed += OnLogFileChanged;
                mFileWatcher.Error += OnFileWatcherError;
                mFileWatcher.EnableRaisingEvents = IsActive;

                ReadNewLogMessagesFromFile();
            }
        }

        /// <summary>
        /// Resets the <see cref="ILogProvider"/> instance.
        /// </summary>
        public override void Clear()
        {
            mLogNumber = 0;
        }

        /// <summary>
        /// Resets the <see cref="ILogProvider"/> instance.
        /// </summary>
        public override void Reset()
        {
            Shutdown();
            Initialize( mLogHandler );
        }

        /// <summary>
        /// Shuts down the <see cref="ILogProvider"/> instance.
        /// </summary>
        public override void Shutdown()
        {
            base.Shutdown();

            if( mFileWatcher != null ) {
                mFileWatcher.EnableRaisingEvents = false;
                mFileWatcher.Changed -= OnLogFileChanged;
                mFileWatcher.Error -= OnFileWatcherError;
                mFileWatcher.Dispose();
            }

            if( mFileReader != null ) {
                mFileReader.Close();
                mFileReader = null;
            }
        }

        /// <summary>
        /// Gets the header used for the CSV file export.
        /// </summary>
        /// <returns></returns>
        public override string GetCsvHeader()
        {
            return "\"Number\","
                 + "\"Level\","
                 + "\"Timestamp\","
                 + "\"Logger\","
                 + "\"Thread\","
                 + "\"Message\","
                 + "\"Location\","
                 + "\"Custom Data\""
                 + Environment.NewLine;
        }

        /// <summary>
        /// Determines whether the <see cref="ReceiverBase"/> instance can handle the given file name as log file.
        /// </summary>
        /// <returns><c>True</c> if the file can be handled, otherwise <c>false</c>.</returns>
        public override bool CanHandleLogFile()
        {
            if( string.IsNullOrEmpty( mFileToObserve ) || !File.Exists( mFileToObserve ) ) {
                return false;
            }

            using( StreamReader tmpReader = new StreamReader( new FileStream(
                mFileToObserve
              , FileMode.Open
              , FileAccess.Read
              , FileShare.ReadWrite ) ) ) {
                string firstLine = tmpReader.ReadLine();

                return !string.IsNullOrEmpty( firstLine )
                    && firstLine.Contains( "<log4j:event" );
            }
        }

        /// <summary>
        /// Saves the current docking layout of the <see cref="ReceiverBase"/> instance.
        /// </summary>
        /// <param name="layout">The layout as string to save.</param>
        public override void SaveLayout( string layout )
        {
            Properties.Settings.Default.DockLayoutLog4NetFileReceiver = layout ?? string.Empty;
            Properties.Settings.Default.SaveSettings();
        }

        /// <summary>
        /// Loads the docking layout of the <see cref="ReceiverBase"/> instance.
        /// </summary>
        /// <returns>The restored layout, or <c>null</c> if none exists.</returns>
        public override string LoadLayout()
        {
            return Properties.Settings.Default.DockLayoutLog4NetFileReceiver;
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new and empty instance of the <see cref="Log4NetFileReceiver"/> class.
        /// </summary>
        public Log4NetFileReceiver()
        {

        }

        /// <summary>
        /// Creates a new and configured instance of the <see cref="Log4NetFileReceiver"/> class.
        /// </summary>
        /// <param name="fileToObserve">The file the new <see cref="Log4NetFileReceiver"/> instance should observe.</param>
        /// <param name="startFromBeginning">Determines whether the new <see cref="Log4NetFileReceiver"/> should read the given <paramref name="fileToObserve"/> from beginnin, or not.</param>
        public Log4NetFileReceiver( string fileToObserve, bool startFromBeginning )
        {
            mFileToObserve = fileToObserve;
            mStartFromBeginning = startFromBeginning;
        }

        #endregion
    }
}
