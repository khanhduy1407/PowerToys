// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Linq;
using interop;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Input;
using Peek.Common.Constants;
using Peek.FilePreviewer.Models;
using Peek.UI.Extensions;
using Peek.UI.Helpers;
using Peek.UI.Native;
using Windows.Foundation;
using WinUIEx;

namespace Peek.UI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : WindowEx
    {
        public MainWindow()
        {
            InitializeComponent();

            ViewModel = new MainWindowViewModel();

            NativeEventWaiter.WaitForEventLoop(Constants.ShowPeekEvent(), OnPeekHotkey);

            TitleBarControl.SetTitleBarToWindow(this);

            AppWindow.Closing += AppWindow_Closing;
        }

        public MainWindowViewModel ViewModel { get; }

        /// <summary>
        /// Handle Peek hotkey, by toggling the window visibility and querying files when necessary.
        /// </summary>
        private void OnPeekHotkey()
        {
            if (AppWindow.IsVisible)
            {
                if (IsNewSingleSelectedItem())
                {
                    Initialize();
                }
                else
                {
                    Uninitialize();
                }
            }
            else
            {
                Initialize();
            }
        }

        private void LeftNavigationInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            ViewModel.AttemptLeftNavigation();
        }

        private void RightNavigationInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            ViewModel.AttemptRightNavigation();
        }

        private void Initialize()
        {
            ViewModel.FolderItemsQuery.Start();
            ViewModel.ScalingFactor = this.GetMonitorScale();
        }

        private void Uninitialize()
        {
            this.Restore();
            this.Hide();

            // TODO: move into general ViewModel method when needed
            ViewModel.FolderItemsQuery.Clear();
        }

        /// <summary>
        /// Handle FilePreviewerSizeChanged event to adjust window size and position accordingly.
        /// </summary>
        /// <param name="sender">object</param>
        /// <param name="e">PreviewSizeChangedArgs</param>
        private void FilePreviewer_PreviewSizeChanged(object sender, PreviewSizeChangedArgs e)
        {
            var monitorSize = this.GetMonitorSize();

            // If no size is requested, try to fit to the monitor size.
            Size requestedSize = e.WindowSizeRequested ?? monitorSize;

            double titleBarHeight = TitleBarControl.ActualHeight;

            double maxContentWidth = monitorSize.Width * WindowConstants.MaxWindowToMonitorRatio;
            double maxContentHeight = (monitorSize.Height - titleBarHeight) * WindowConstants.MaxWindowToMonitorRatio;
            Size maxContentSize = new(maxContentWidth, maxContentHeight);

            double minContentWidth = WindowConstants.MinWindowWidth;
            double minContentHeight = WindowConstants.MinWindowHeight - titleBarHeight;
            Size minContentSize = new(minContentWidth, minContentHeight);

            Size adjustedContentSize = requestedSize.Fit(maxContentSize, minContentSize);

            // TODO: Only re-center if window has not been resized by user (or use design-defined logic).
            // TODO: Investigate why portrait images do not perfectly fit edge-to-edge
            double monitorScale = this.GetMonitorScale();
            double scaledWindowWidth = adjustedContentSize.Width / monitorScale;
            double scaledWindowHeight = adjustedContentSize.Height / monitorScale;

            this.CenterOnScreen(scaledWindowWidth + WindowConstants.WindowHeightContentPadding, scaledWindowHeight + titleBarHeight + WindowConstants.WindowWidthContentPadding);
            this.Show();
            this.BringToForeground();
        }

        /// <summary>
        /// Handle AppWindow closing to prevent app termination on close.
        /// </summary>
        /// <param name="sender">AppWindow</param>
        /// <param name="args">AppWindowClosingEventArgs</param>
        private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            args.Cancel = true;
            Uninitialize();
        }

        private bool IsNewSingleSelectedItem()
        {
            var foregroundWindowHandle = Windows.Win32.PInvoke.GetForegroundWindow();

            var selectedItems = FileExplorerHelper.GetSelectedItems(foregroundWindowHandle);
            var selectedItemsCount = selectedItems?.GetCount() ?? 0;
            if (selectedItems == null || selectedItemsCount == 0 || selectedItemsCount > 1)
            {
                return false;
            }

            var fileExplorerSelectedItemPath = selectedItems.GetItemAt(0).ToIFileSystemItem().Path;
            var currentItemPath = ViewModel.FolderItemsQuery.CurrentItem?.Path;
            if (fileExplorerSelectedItemPath == null || currentItemPath == null || fileExplorerSelectedItemPath == currentItemPath)
            {
                return false;
            }

            return true;
        }
    }
}