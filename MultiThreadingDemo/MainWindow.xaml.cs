/******************************************************************************
 * Filename    = MainWindow.xaml.cs
 *
 * Author      = Ramaswamy Krishnan-Chittur
 *
 * Product     = MultiThreadingDemo
 * 
 * Project     = MultiThreadingDemo
 *
 * Description = A project to demonstrate multithreading and synchronization.
 *****************************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MultiThreadingDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// # Design
    /// The application has a simple WPF-based GUI, and supports sending and receiving messages through UDP.
    /// - The application runs a WPF based GUI.
    /// - A worker thread is spawned to listen for messages on a UDP port.
    /// - Another worker thread is spawned to summarize messages after a batch of messages has been transacted.
    /// - A task is used to send messages to a UDP port.
    /// - Synchronization primitives are used to make thread-safe access of shared resources.
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<string> _messages;             // List of messages that have been sent and received.
        private readonly int _listenPort;           // Port that we are listening on.
        private readonly Thread _listenThread;      // Thread that listens for messages on the port.
        private readonly Thread _summarizerThread;  // Thread that summarizes context from the list of messages.
        private AutoResetEvent _triggerSummarize;   // Event to notify that summarizer needs to be run.
        private const int TriggerSummarizerMessageCount = 5; // Batch count of messages after which to run summarizer.

        /// <summary>
        /// Creates an instance of the main window.
        /// </summary>
        public MainWindow()
        {
            Debug.WriteLine($"Main Thread Id = {Environment.CurrentManagedThreadId}.");

            InitializeComponent();

            // Initializes the list of messages.
            _messages = new();

            // Creates the auto-reset event that is triggered when summarizer should run.
            _triggerSummarize = new(false);

            // Pick a random port to listen for messages, and display it.
            _listenPort = new Random().Next(10000, 65000);
            ReceivePortTextBox.Text = _listenPort.ToString();

            // Create and start the thread that listens for messages.
            _listenThread = new(new ThreadStart(ListenerThreadProc))
            {
                IsBackground = true // Stop the thread when the main thread stops.
            };
            _listenThread.Start();

            // Create and start the thread that summarizes the messages.
            _summarizerThread = new(new ThreadStart(SummarizerThreadProc))
            {
                IsBackground = true // Stop the thread when the main thread stops.
            };
            _summarizerThread.Start();
        }

        /// <summary>
        /// Listens for messages on the listening port.
        /// </summary>
        private void ListenerThreadProc()
        {
            Debug.WriteLine($"Listener Thread Id = {Environment.CurrentManagedThreadId}.");

            try
            {
                using UdpClient listener = new(_listenPort);
                IPEndPoint endPoint = new(IPAddress.Any, _listenPort);
                while (true)
                {
                    // Listen for message on the listening port, and receive it when it comes along.
                    byte[] bytes = listener.Receive(ref endPoint);
                    string message = Encoding.ASCII.GetString(bytes, 0, bytes.Length);

                    int totalMessageCount; // Total count of messages that have been sent and received.

                    // Add the message to the message collection.
                    lock (this) // Synchronize access to the shared field.
                    {
                        _messages.Add(message);
                        totalMessageCount = _messages.Count;
                    }

                    // Display the message and count.
                    // Make sure however that the UI interaction is done from the UI thread.
                    _ = Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        ReceiveTextBox.Text = message;
                        TotalMessageCountTextBox.Text = totalMessageCount.ToString();
                    });

                    // Trigger the summarizer if required.
                    TriggerSummarizerIfRequired(totalMessageCount);
                }
            }
            catch (Exception exception)
            {
                // In case of error, make sure to dispatch it to the UI thread for displaying.
                _ = Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _ = MessageBox.Show(exception.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    ReceivePortTextBox.Text = string.Empty;
                    ReceiveTextBox.Text = "Error: Could not setup listener.";
                });
            }
        }

        /// <summary>
        /// Waits for a trigger and generates summary from the list of messages.
        /// </summary>
        private void SummarizerThreadProc()
        {
            Debug.WriteLine($"Summarizer Thread Id = {Environment.CurrentManagedThreadId}.");

            while (true)
            {
                // Wait for the trigger to generate the summary.
                _triggerSummarize.WaitOne();

                // Synchronize and make a copy of the list of messages, so that we can release the lock on the shared field.
                List<string> messages = new();
                lock (this)
                {
                    messages.AddRange(_messages);
                }

                // To do: Use Machine Learning algorithms to generate a good summary.
                // To keep it simple here, simply find the longest message as use that as a summary.
                string longest = string.Empty;
                foreach (string message in messages)
                {
                    if (longest.Length < message.Length)
                    {
                        longest = message;
                    }
                }

                // Dispatch the summary to the UI thread for display.
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SummaryTextBox.Text = longest;
                });
            }
        }

        /// <summary>
        /// Handles the 'send message' button click event.
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="e">Button click event args</param>
        private async void SendMessageButtonClick(object sender, RoutedEventArgs e)
        {
            SendMessageButton.IsEnabled = false; // Disable the button until we process this message.

            try
            {
                // Get the message and the sending port details.
                int port = Convert.ToInt32(SendPortTextBox.Text);
                string message = SendMessageTextBox.Text;

                // Dispatch the sending of the message to a task.
                // Await on the task to finish, but that will still let the UI be responsive in the meanwhile.
                bool result = await Task.Run(() => SendMessage(port, message));
                Debug.WriteLine($"Result of sending of message {message} to {port} was {result}.");

                int totalMessageCount; // Total count of messages that have been sent and received.

                // Add the message to the message collection.
                lock (this) // Synchronize access to the shared field.
                {
                    _messages.Add(message);
                    totalMessageCount = _messages.Count;
                }

                // Update the UI with the message count.
                TotalMessageCountTextBox.Text = totalMessageCount.ToString();

                // Trigger the summarizer if required.
                TriggerSummarizerIfRequired(totalMessageCount);
            }
            catch(Exception exception)
            {
                _ = MessageBox.Show(exception.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            SendMessageButton.IsEnabled = true; // Re-enable the button now that we have processed the message.
        }

        /// <summary>
        /// Sends the given message to the given port on the local device.
        /// </summary>
        /// <param name="port">Port to send the message to</param>
        /// <param name="message">Message to be sent</param>
        /// <returns>A value indicating whether the sending was successful</returns>
        private static bool SendMessage(int port, string message)
        {
            const string LocalHost = "127.0.0.1"; // IP address for the local device.
            Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPAddress broadcastAddress = IPAddress.Parse(LocalHost);
            byte[] sendBuffer = Encoding.ASCII.GetBytes(message);
            IPEndPoint endPoint = new(broadcastAddress, port);
            int bytesSent = socket.SendTo(sendBuffer, endPoint);
            return (bytesSent == sendBuffer.Length);
        }

        /// <summary>
        /// Triggers the summarizer if required.
        /// We do not want to summarize on every message. Only after a batch of certain number of messages.
        /// </summary>
        /// <param name="totalMessageCount">Total number of messages sent and received</param>
        private void TriggerSummarizerIfRequired(int totalMessageCount)
        {
            // Set the event to tell the summarizer thread to start summarizing.
            // We do not want to summarize on every message. Only after a batch of certain number of messages.
            if ((totalMessageCount % TriggerSummarizerMessageCount) == 0)
            {
                _triggerSummarize.Set();
            }
        }
    }
}
