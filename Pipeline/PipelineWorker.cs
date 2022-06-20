using org.daisy.pipeline;
using System.Collections.Concurrent;

using org.daisy.pipeline.script;
using static org.daisy.Pipeline;

namespace org.daisy
{
    /// <summary>
    /// Worker class to manage pipeline instancing and execution on separate thread, <br/> 
    /// Required for async environment to avoid pointer corruption
    /// </summary>
    public static class PipelineWorker
    {
        public static string Output;
        public static string Errors;

        // Pipeline output events

        private static event PipelineOutputListener OnPipelineOutputEvent = (message) => { Output += message + "\r\n"; };
        public static void AddPipelineOutputListener(PipelineOutputListener onPipelineOutput)
        {
            OnPipelineOutputEvent += onPipelineOutput;
            // TODO : send an event to reload the event listener on the pipeline instance
        }
        public static PipelineOutputListener OnPipelineOutput => OnPipelineOutputEvent;

        // Pipeline errors events
        private static event PipelineErrorListener OnPipelineErrorEvent = (message) => { Errors += message + "\r\n"; };
        public static void AddPipelineErrorListener(PipelineErrorListener onPipelineError)
        {
            OnPipelineErrorEvent += onPipelineError;
            // TODO : send an event to reload the event listener on the pipeline instance
        }

        public static PipelineErrorListener OnPipelineError => OnPipelineErrorEvent;

        public static ConcurrentDictionary<string, pipeline.Script> Scripts = new ConcurrentDictionary<string, pipeline.Script>();

        /// <summary>
        /// </summary>
        public static ConcurrentDictionary<string, pipeline.Job> Jobs = new ConcurrentDictionary<string, pipeline.Job>();

        public static ConcurrentQueue<PipelineEventArgs> Events = new ConcurrentQueue<PipelineEventArgs>();

        public enum TasksList
        {
            //GetScripts,
            LaunchNewJob,
            //GetJobStatus,
        }

        public class PipelineEventArgs : EventArgs
        {
            public PipelineEventArgs(TasksList task, params object[] parameters)
            {
                Task = task;
                Parameters = parameters;
            }

            public TasksList Task;

            public object[]? Parameters;
        }

        private static Thread _worker = null;

        public enum StateValue
        {
            Stopping,
            Stopped,
            Starting,
            Ready,
            Working,
        }

        public static StateValue State = StateValue.Stopped;

        public static bool stopProcess { get; set; } = false;

        public static bool isReady { get; set; } = false;


        /// <summary>
        /// Run the pipeline in a single contained function for thread/memory safety
        /// and possible memory release
        /// </summary>
        private static void run()
        {
            Pipeline instance = Pipeline.Instance;
            
            // First retrieve scripts lists
            Scripts["dtbook-to-daisy3"] = new pipeline.Script("dtbook-to-daisy3")
            {
                Description = "Testing script",
                Name = "Conversion of dtbook3 to daisy3",
                Parameters = new List<Parameter>()
                {
                    new Parameter("source",Parameter.TypeCategory.Input)
                    {
                        DataTypeValue = Parameter.DataType.Value.File,
                        Primary = true,
                    },
                    new Parameter("validation-status", Parameter.TypeCategory.Output)
                    {
                        DataTypeValue = Parameter.DataType.Value.String,

                    },
                    new Parameter("include-tts-log", Parameter.TypeCategory.Option)
                    {
                        DataTypeValue = Parameter.DataType.Value.Boolean,
                    },
                    new Parameter("tts-log",Parameter.TypeCategory.Output)
                    {
                        DataTypeValue = Parameter.DataType.Value.String,

                    },
                    new Parameter( "publisher",Parameter.TypeCategory.Option)
                    {
                        DataTypeValue = Parameter.DataType.Value.String,
                    },
                    new Parameter("output-dir",Parameter.TypeCategory.Option)
                    {
                        DataTypeValue = Parameter.DataType.Value.Directory,
                        Required = true,
                    },
                    new Parameter("tts-config", Parameter.TypeCategory.Input )
                    {
                        DataTypeValue = Parameter.DataType.Value.File,
                    },
                    new Parameter("audio", Parameter.TypeCategory.Option)
                    {
                        DataTypeValue = Parameter.DataType.Value.Boolean,
                        DefaultValue = false
                    },
                    new Parameter("with-text", Parameter.TypeCategory.Option)
                    {
                        DataTypeValue = Parameter.DataType.Value.Boolean,
                        DefaultValue = true
                    },

                }
            };
            instance.SetPipelineErrorListener(OnPipelineError);
            instance.SetPipelineOutputListener(OnPipelineOutput);
            isReady = true;
            while (!stopProcess)
            {
                try
                {
                    State = StateValue.Ready;
                    PipelineEventArgs parsedEvent;
                    while(Events.TryDequeue(out parsedEvent))
                    {
                        State = StateValue.Working;
                        switch (parsedEvent.Task)
                        {
                            case TasksList.LaunchNewJob:
                                if(parsedEvent.Parameters != null && parsedEvent.Parameters.Length > 0)
                                {
                                    Job toRun = (Job)parsedEvent.Parameters[0];
                                    try
                                    {
                                        IntPtr javaJob = toRun.run(ref instance);
                                        Jobs[toRun.ID] = toRun;
                                    } catch (Exception e)
                                    {

                                        throw;
                                    }
                                    
                                }
                                break;
                            default:
                                break;
                        }
                    }
                    // Parse event queue
                    // Also update all jobs that are

                    // For each job in the running queue, retrieve updates
                    foreach (var item in Jobs)
                    {
                        if (item.Value.Running)
                        {
                            State = StateValue.Working;
                            item.Value.requestUpdate(ref instance);
                        }
                    }
                } catch (Exception e)
                {
                    OnPipelineError?.Invoke(e.Message);
                    //throw;
                }
                Thread.Sleep(100);
            }
            State = StateValue.Stopped;
            isReady = false;
        }

        public static Job launch(
            Script toRun,
            Dictionary<string, object> parameters,
            EventHandler<pipeline.job.JobUpdateEventArgs>? onJobUpdateCallback )
        {
            if(_worker == null)
            {
                Start();
            }
            while (State < StateValue.Ready)
            {
                Thread.Sleep(100);
            }
            Job newJob = new Job(toRun, parameters);
            newJob.onUpdate = onJobUpdateCallback;
            Events.Enqueue(new PipelineEventArgs(TasksList.LaunchNewJob, new object[]
            {
                newJob
            }));
            
            return newJob;
        }

        /// <summary>
        /// Launch the worker thread
        /// </summary>
        public static void Start()
        {
            if(_worker == null)
            {
                _worker = new Thread(run);
                _worker.Start();
            } else if (_worker.ThreadState != ThreadState.Running)
            {
                _worker.Join();
                _worker = new Thread(run);
                _worker.Start();
            }
        }

        public static void Stop(bool forced = false)
        {
            if (_worker != null)
            {   
                stopProcess = true;
                State = StateValue.Stopping;
                if (forced)
                {
                    Pipeline.KillInstance();
                    _worker = null;
                    State = StateValue.Stopped;
                }
            }
        }




    }
}
