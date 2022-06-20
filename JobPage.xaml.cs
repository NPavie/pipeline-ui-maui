namespace org.daisy.pipeline.ui;

public partial class JobPage : ContentPage
{
    public pipeline.Job Job { get; }
    public JobPage()
	{
		InitializeComponent();
	}

    public void onJobUpdate(object jobObject, job.JobUpdateEventArgs e)
    {
        // Update progress, logs and set output lists
        this.Status.Text = e.NewStatus.ToString();
        if (e.Messages != null)
        {
            this.Logs.Text = string.Join("\r\n", e.Messages);
        } else if (Job.Messages.Count > 0)
        {
            this.Logs.Text = string.Join("\r\n", Job.Messages);
        }
        // Also send notifications on status change
        if (e.OldStatus != e.NewStatus)
        {
            Services.INotificationService notif = ServiceProvider.Current.GetService<Services.INotificationService>();
            if(notif != null)
            {
                notif.ShowNotification($"Daisy pipeline job {Job.ID}", "Status changed to " + e.NewStatus.ToString());
            }
            
            
        }
    }

    public JobPage(pipeline.Job job) : this()
    {
        Job = job;
        Title = $"Job {job.ID}";
        ScriptName.Text = job.Script.ID;

        if(Job.Messages != null && Job.Messages.Count > 0)
        {
            this.Logs.Text = string.Join("\r\n", Job.Messages);
        }
        

        Job.onUpdate = onJobUpdate;
        // retrieve job input, parameters, status and possibly outputs
        foreach (var item in job.Parameters)
        {
            JobParameters.Add(new HorizontalStackLayout()
            {
                Children =
                {
                    new Label() {Text = item.Key + " : "},
                    new Label() {Text = item.Value.ToString()}
                }
            });
        }
    }

    //public JobPage(IntPtr jobPointer) : this()
    //{
    //    // retrieve job input, parameters, status and possibly outputs from java
    //    Job = new Job(jobPointer);
    //    Job.onUpdate = onJobUpdate;

    //}
}