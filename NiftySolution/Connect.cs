// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.
using System;
using Extensibility;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.CommandBars;
using System.Resources;
using System.Reflection;
using System.Globalization;
using System.Collections.Generic;
using System.IO;

// Setup stuff taken from: http://blogs.msdn.com/jim_glass/archive/2005/08/18/453218.aspx
namespace Aurora
{
	namespace NiftySolution
	{
		// This class is the required interface to Visual Studio. 
		// Simple a very lightweight wrapper around the plugin object.
		public class Connect : IDTExtensibility2, IDTCommandTarget
		{
			private Plugin m_plugin;
			private SolutionBuildTimings m_timings;
			private CommandRegistry m_commandRegistry;
			private DebuggerEvents m_debuggerEvents;

			public Connect()
			{
			}

			public void OnConnection(object application_, ext_ConnectMode connectMode, object addInInst, ref Array custom)
			{
				if(null != m_plugin)
					return;

				// Load up the options from file.
				string optionsFileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "NiftySolution.xml");
				Options options = Options.Load(optionsFileName);

				// Create our main plugin facade.
				DTE2 application = (DTE2)application_;
				m_plugin = new Plugin(application, (AddIn)addInInst, "NiftySolution", "Aurora.NiftySolution.Connect", options);
				
				// Every plugin needs a command bar.
				CommandBar commandBar = m_plugin.AddCommandBar("NiftySolution", MsoBarPosition.msoBarTop);
				m_commandRegistry = new CommandRegistry(m_plugin, commandBar);

				// Initialize the logging system.
				if(Log.HandlerCount == 0)
				{
					#if DEBUG
					Log.AddHandler(new DebugLogHandler());
					#endif

					Log.AddHandler(new VisualStudioLogHandler(m_plugin.OutputPane));
					Log.Prefix = "NiftySolution";
				}

				// Now we can take care of registering ourselves and all our commands and hooks.
				Log.Debug("Booting up...");
				Log.IncIndent();


				bool doBindings = options.EnableBindings;

				m_commandRegistry.RegisterCommand("NiftyOpen", doBindings, new QuickOpen(m_plugin));
				m_commandRegistry.RegisterCommand("NiftyToggle", doBindings, new ToggleFile(m_plugin));
				m_commandRegistry.RegisterCommand("NiftyClose", doBindings, new CloseToolWindow(m_plugin));
				m_commandRegistry.RegisterCommand("NiftyConfigure", doBindings, new Configure(m_plugin));

                if (options.SilentDebuggerExceptions || options.IgnoreDebuggerExceptions)
                {
                    m_debuggerEvents = application.Events.DebuggerEvents;
                    m_debuggerEvents.OnExceptionNotHandled += new _dispDebuggerEvents_OnExceptionNotHandledEventHandler(OnExceptionNotHandled);
                }

				m_timings = new SolutionBuildTimings(m_plugin);

				Log.DecIndent();
				Log.Debug("Initialized...");
			}

			public void OnDisconnection(ext_DisconnectMode disconnectMode, ref Array custom)
			{
				if(null == m_plugin)
					return;

				Log.Debug("Disconnect called...");
				((Options)m_plugin.Options).Save();
				Log.ClearHandlers();
			}

			public void OnAddInsUpdate(ref Array custom)
			{
			}

			public void OnStartupComplete(ref Array custom)
			{
			}

			public void OnBeginShutdown(ref Array custom)
			{
			}

			public void QueryStatus(string commandName, vsCommandStatusTextWanted neededText, ref vsCommandStatus status, ref object commandText)
			{
				if(null == m_plugin || null == m_commandRegistry)
					return;

				if(neededText != vsCommandStatusTextWanted.vsCommandStatusTextWantedNone)
					return;
				
				status = m_commandRegistry.Query(commandName);
			}

			public void Exec(string commandName, vsCommandExecOption executeOption, ref object varIn, ref object varOut, ref bool handled)
			{
				handled = false;

				if(null == m_plugin || null == m_commandRegistry)
					return;
				if(executeOption != vsCommandExecOption.vsCommandExecOptionDoDefault)
					return;

				handled = m_commandRegistry.Execute(commandName);
			}

            protected void OnExceptionNotHandled(string exceptionType, string name, int code, string description, ref dbgExceptionAction exceptionAction)
            {
                Options options = ((Options)m_plugin.Options);

                if (options.IgnoreDebuggerExceptions)
                {
                    exceptionAction = dbgExceptionAction.dbgExceptionActionContinue;
                }
                else if (options.SilentDebuggerExceptions)
                {
                    exceptionAction = dbgExceptionAction.dbgExceptionActionBreak;
                }
            }
        }
	}
}
