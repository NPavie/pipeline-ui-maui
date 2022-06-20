﻿namespace org.daisy.pipeline.ui;

public partial class AppShell : Shell
{

    public AppShell()
    {
        InitializeComponent();

        MessagingCenter.Subscribe<NewJobPage, Job>(this, "New job", async (sender, job) =>
        {
            
            Jobs.Items.Add(
                new ShellContent()
                {
                    Title = $"Job {job.ID}",
                    Route = $"job{job.ID}",
                    Content = new JobPage(job),
                }
            );

            // Remove the previous empty shell page after adding the first job
            if (NoJobs.IsVisible)
            {
                NoJobs.IsVisible = false;
            }
            await GoToAsync($"///jobs/job{job.ID}");
        });
    }

    protected override void OnNavigating(ShellNavigatingEventArgs args)
    {
        base.OnNavigating(args);
        
    }

    protected override void OnNavigated(ShellNavigatedEventArgs args)
    {
        base.OnNavigated(args);
        if (Shell.Current != null && Shell.Current.CurrentPage != null)
        {
            this.Title = "DAISY Pipeline 2 - " + (Shell.Current.CurrentPage.Title).ToString();
        }

    }
}
