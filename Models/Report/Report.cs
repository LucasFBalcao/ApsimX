﻿// -----------------------------------------------------------------------
// <copyright file="Report.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
//-----------------------------------------------------------------------
namespace Models.Report
{
    using APSIM.Shared.Utilities;
    using Models.Core;
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// A report class for writing output to the data store.
    /// </summary>
    [Serializable]
    [ViewName("UserInterface.Views.ReportView")]
    [PresenterName("UserInterface.Presenters.ReportPresenter")]
    [ValidParent(ParentType = typeof(Zone))]
    [ValidParent(ParentType = typeof(Zones.CircularZone))]
    [ValidParent(ParentType = typeof(Zones.RectangularZone))]
    [ValidParent(ParentType = typeof(Simulation))]
    public class Report : Model
    {
        /// <summary>The columns to write to the data store.</summary>
        private List<IReportColumn> columns = null;

        /// <summary>An array of column names to write to storage.</summary>
        private IEnumerable<string> columnNames = null;

        /// <summary>An array of columns units to write to storage.</summary>
        private IEnumerable<string> columnUnits = null;

        /// <summary>Link to a simulation</summary>
        [Link]
        private Simulation simulation = null;

        /// <summary>Link to a clock model.</summary>
        [Link]
        private IClock clock = null;

        /// <summary>Link to a storage service.</summary>
        [Link]
        private IStorageWriter storage = null;

        /// <summary>Link to a locator service.</summary>
        [Link]
        private ILocator locator = null;

        /// <summary>Link to an event service.</summary>
        [Link]
        private IEvent events = null;

        /// <summary>Experiment factor names</summary>
        public List<string> ExperimentFactorNames { get; set; }

        /// <summary>Experiment factor values</summary>
        public List<string> ExperimentFactorValues { get; set; }

        /// <summary>
        /// Gets or sets variable names for outputting
        /// </summary>
        [Summary]
        [Description("Output variables")]
        public string[] VariableNames { get; set; }

        /// <summary>
        /// Gets or sets event names for outputting
        /// </summary>
        [Summary]
        [Description("Output frequency")]
        public string[] EventNames { get; set; }

        /// <summary>An event handler to allow us to initialize ourselves.</summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>
        [EventSubscribe("Commencing")]
        private void OnCommencing(object sender, EventArgs e)
        {
            // Subscribe to events.
            foreach (string eventName in EventNames)
            {
                if (eventName != string.Empty)
                    events.Subscribe(eventName.Trim(), DoOutput);
            }

            // sanitise the variable names and remove duplicates
            List<string> variableNames = new List<string>();
            variableNames.Add("Name as Zone");
            for (int i = 0; i < this.VariableNames.Length; i++)
            {
                bool isDuplicate = StringUtilities.IndexOfCaseInsensitive(variableNames, this.VariableNames[i].Trim()) != -1;
                if (!isDuplicate && this.VariableNames[i] != string.Empty)
                    variableNames.Add(this.VariableNames[i].Trim());
            }
            this.VariableNames = variableNames.ToArray();
            this.FindVariableMembers();
        }

        /// <summary>A method that can be called by other models to perform a line of output.</summary>
        public void DoOutput(object sender, EventArgs args)
        {
            object[] valuesToWrite = new object[columns.Count];
            for (int i = 0; i < columns.Count; i++)
                valuesToWrite[i] = columns[i].GetValue();
            storage.WriteRow(simulation.Name, Name, columnNames, columnUnits, valuesToWrite);
        }

        /// <summary>Create a text report from tables in this data store.</summary>
        /// <param name="storage">The data store.</param>
        /// <param name="fileName">Name of the file.</param>
        public static void WriteAllTables(IStorageReader storage, string fileName)
        {
            // Write out each table for this simulation.
            foreach (string tableName in storage.TableNames)
            {
                DataTable data = storage.GetData(tableName);
                if (data != null && data.Rows.Count > 0)
                {
                    StreamWriter report = new StreamWriter(Path.ChangeExtension(fileName, "." + tableName + ".csv"));
                    DataTableUtilities.DataTableToText(data, 0, ",", true, report);
                    report.Close();
                }
            }
        }

        /// <summary>
        /// Fill the Members list with VariableMember objects for each variable.
        /// </summary>
        private void FindVariableMembers()
        {
            this.columns = new List<IReportColumn>();

            AddExperimentFactorLevels();

            foreach (string fullVariableName in this.VariableNames)
            {
                if (fullVariableName != string.Empty)
                    this.columns.Add(ReportColumn.Create(fullVariableName, clock, storage, locator, events));
            }
            columnNames = columns.Select(c => c.Name);
            columnUnits = columns.Select(c => c.Units);
        }

        /// <summary>Add the experiment factor levels as columns.</summary>
        private void AddExperimentFactorLevels()
        {
            if (ExperimentFactorValues != null)
            {
                for (int i = 0; i < ExperimentFactorNames.Count; i++)
                    this.columns.Add(new ReportColumnConstantValue(ExperimentFactorNames[i], ExperimentFactorValues[i]));
            }
        }
 
    }
}