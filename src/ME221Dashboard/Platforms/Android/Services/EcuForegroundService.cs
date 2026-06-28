using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;

namespace ME221Dashboard.Platforms.Android.Services;

/// <summary>
/// Foreground service that keeps the ECU connection alive when the activity is paused
/// (e.g. during file picker intents). Without this, Android may suspend USB devices
/// and kill network sockets when the activity goes to background.
/// </summary>
[Service(
    Name = "com.scippo97sensibleproductions.me221dashboard.EcuForegroundService",
    ForegroundServiceType = ForegroundService.TypeDataSync,
    Exported = false)]
public class EcuForegroundService : Service
{
    private const string ChannelId = "ecu_connection_channel";
    private const string ChannelName = "ECU Connection";
    private const int NotificationId = 1;

    public static bool IsRunning { get; private set; }

    public override void OnCreate()
    {
        base.OnCreate();
        var channel = new NotificationChannel(ChannelId, ChannelName, NotificationImportance.Low)
        {
            Description = "Keeps ECU connection alive during file operations"
        };
        var manager = (NotificationManager)GetSystemService(NotificationService)!;
        manager.CreateNotificationChannel(channel);
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == "STOP")
        {
            StopForeground(StopForegroundFlags.Remove);
            StopSelf();
            IsRunning = false;
            return StartCommandResult.NotSticky;
        }

        try
        {
            var builder = new NotificationCompat.Builder(this!, ChannelId);
            builder.SetContentTitle("ME221 Dashboard");
            builder.SetContentText("ECU connection active");
            builder.SetSmallIcon(global::Android.Resource.Drawable.IcMenuManage);
            builder.SetPriority(NotificationCompat.PriorityLow);
            builder.SetOngoing(true);
            builder.SetSilent(true);
            var notification = builder.Build();

            StartForeground(NotificationId, notification);
            IsRunning = true;
        }
        catch (Exception)
        {
            // Foreground service failed (missing permissions, etc.) — continue without it
            IsRunning = false;
        }

        return StartCommandResult.Sticky;
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnDestroy()
    {
        IsRunning = false;
        StopForeground(StopForegroundFlags.Remove);
        base.OnDestroy();
    }

    public static void Start(Context context)
    {
        if (IsRunning) return;
        try
        {
            var intent = new Intent(context, typeof(EcuForegroundService));
            intent.SetAction("START");
            context.StartForegroundService(intent);
        }
        catch (Exception)
        {
            // Service start failed — proceed without foreground service
        }
    }

    public static void Stop(Context context)
    {
        if (!IsRunning) return;
        try
        {
            var intent = new Intent(context, typeof(EcuForegroundService));
            intent.SetAction("STOP");
            context.StartService(intent);
        }
        catch (Exception)
        {
            // Best effort stop
        }
    }
}
