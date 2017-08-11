﻿namespace Models.Core.Runners
{
    using APSIM.Shared.Utilities;
    using Factorial;
    using Storage;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    /// <summary>
    /// A runnable job that looks at the model passed in and determines what is to be run.
    /// Will spawn other jobs to do the actual running.
    /// </summary>
    class RunOrganiser : JobManager.IRunnable
    {
        private Simulations simulations;
        private Model model;
        private bool runTests;

        /// <summary>Constructor</summary>
        /// <param name="model">The model to run.</param>
        /// <param name="simulations">simulations object.</param>
        /// <param name="runTests">Run the test nodes?</param>
        public RunOrganiser(Simulations simulations, Model model, bool runTests)
        {
            this.simulations = simulations;
            this.model = model;
            this.runTests = runTests;
        }

        /// <summary>Called to start the job.</summary>
        /// <param name="jobManager">The job manager running this job.</param>
        /// <param name="workerThread">The thread this job is running on.</param>
        public void Run(JobManager jobManager, BackgroundWorker workerThread)
        {
            JobSequence parentJob = new JobSequence();
            JobParallel simulationJobs = new JobParallel();
            List<string> simulationNames = new List<string>();
            FindAllSimulationsToRun(model, simulationJobs, simulationNames);
            parentJob.Jobs.Add(simulationJobs);
            parentJob.Jobs.Add(new RunAllCompletedEvent(simulations));

            ILocator locator = simulations.GetLocatorService(simulations);
            IStorageReader store = locator.Get(typeof(IStorageReader)) as IStorageReader;

            if (store == null)
                throw new Exception("Cannot find a DataStore.");
            store.BeginWriting(simulations.FindAllSimulationNames(), simulationNames);
            
            if (runTests)
            {
                foreach (Tests tests in Apsim.ChildrenRecursively(model, typeof(Tests)))
                    parentJob.Jobs.Add(tests);
            }
            jobManager.AddChildJob(this, parentJob);
        }

        /// <summary>Find simulations/experiments to run.</summary>
        /// <param name="model">The model and its children to search.</param>
        /// <param name="parentJob">The parent job to add the child jobs to.</param>
        /// <param name="simulationNames">Simulations names found.</param>
        private void FindAllSimulationsToRun(IModel model, JobParallel parentJob, List<string> simulationNames)
        { 
            // Get jobs to run and give them to job manager.
            List<JobManager.IRunnable> jobs = new List<JobManager.IRunnable>();

            if (model is Experiment)
            {
                parentJob.Jobs.Add(model as Experiment);
                simulationNames.AddRange((model as Experiment).Names());
            }
            else if (model is Simulation)
            {
                simulationNames.Add(model.Name);
                parentJob.Jobs.Add(new RunSimulation(model as Simulation, simulations, doClone: model.Parent != null));
            }
            else
            {
                // Look for simulations.
                foreach (Model child in model.Children)
                    if (child is Experiment || child is Simulation || child is Folder)
                        FindAllSimulationsToRun(child, parentJob, simulationNames);
            }
        }
    }
}
