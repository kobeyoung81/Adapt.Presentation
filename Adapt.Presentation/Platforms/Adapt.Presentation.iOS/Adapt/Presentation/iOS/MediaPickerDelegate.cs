//
//  Copyright 2011-2013, Xamarin Inc.
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//

using System;
using System.IO;
using System.Threading.Tasks;

using CoreGraphics;
using AssetsLibrary;
using Foundation;
using UIKit;
using NSAction = System.Action;
using System.Globalization;

namespace Adapt.Presentation.iOS
{
    internal class MediaPickerDelegate
        : UIImagePickerControllerDelegate
    {
        internal MediaPickerDelegate(UIViewController viewController, UIImagePickerControllerSourceType sourceType, StoreCameraMediaOptions options)
        {
            this.viewController = viewController;
            source = sourceType;
            this.options = options ?? new StoreCameraMediaOptions();

            if (viewController == null)
            {
                return;
            }

            UIDevice.CurrentDevice.BeginGeneratingDeviceOrientationNotifications();
            observer = NSNotificationCenter.DefaultCenter.AddObserver(UIDevice.OrientationDidChangeNotification, DidRotate);
        }

        public UIPopoverController Popover
        {
            get;
            set;
        }

        public UIView View => viewController.View;

        public Task<MediaFile> Task => tcs.Task;

        public override async void FinishedPickingMedia(UIImagePickerController picker, NSDictionary info)
        {
            RemoveOrientationChangeObserverAndNotifications();

            MediaFile mediaFile;
            switch ((NSString)info[UIImagePickerController.MediaType])
            {
                case MediaImplementation.TypeImage:
                    mediaFile = await GetPictureMediaFile(info);
                    break;

                case MediaImplementation.TypeMovie:
                    mediaFile = await GetMovieMediaFile(info);
                    break;

                default:
                    throw new NotSupportedException();
            }

            if (UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Phone)
            {
                UIApplication.SharedApplication.SetStatusBarStyle(MediaImplementation.StatusBarStyle, false);
            }

            Dismiss(picker, () =>
            {


                tcs.TrySetResult(mediaFile);
            });
        }

        public override void Canceled(UIImagePickerController picker)
        {
            RemoveOrientationChangeObserverAndNotifications();

            if (UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Phone)
            {
                UIApplication.SharedApplication.SetStatusBarStyle(MediaImplementation.StatusBarStyle, false);
            }

            Dismiss(picker, () =>
            {


                tcs.SetResult(null);
            });
        }

        public void DisplayPopover(bool hideFirst = false)
        {
            if (Popover == null)
                return;

            var swidth = UIScreen.MainScreen.Bounds.Width;
            var sheight = UIScreen.MainScreen.Bounds.Height;

            nfloat width = 400;
            nfloat height = 300;


            if (orientation == null)
            {
                orientation = IsValidInterfaceOrientation(UIDevice.CurrentDevice.Orientation) ? UIDevice.CurrentDevice.Orientation : GetDeviceOrientation(viewController.InterfaceOrientation);
            }

            nfloat x, y;
            if (orientation == UIDeviceOrientation.LandscapeLeft || orientation == UIDeviceOrientation.LandscapeRight)
            {
                y = (swidth / 2) - (height / 2);
                x = (sheight / 2) - (width / 2);
            }
            else
            {
                x = (swidth / 2) - (width / 2);
                y = (sheight / 2) - (height / 2);
            }

            if (hideFirst && Popover.PopoverVisible)
                Popover.Dismiss(animated: false);

            Popover.PresentFromRect(new CGRect(x, y, width, height), View, 0, animated: true);
        }

        private UIDeviceOrientation? orientation;
        private readonly NSObject observer;
        private readonly UIViewController viewController;
        private readonly UIImagePickerControllerSourceType source;
        private TaskCompletionSource<MediaFile> tcs = new TaskCompletionSource<MediaFile>();
        private readonly StoreCameraMediaOptions options;

        private bool IsCaptured => source == UIImagePickerControllerSourceType.Camera;

        private void Dismiss(UIImagePickerController picker, NSAction onDismiss)
        {
            if (viewController == null)
            {
                onDismiss();
                tcs = new TaskCompletionSource<MediaFile>();
            }
            else
            {
                if (Popover != null)
                {
                    Popover.Dismiss(animated: true);
                    Popover.Dispose();
                    Popover = null;

                    onDismiss();
                }
                else
                {
                    picker.DismissViewController(true, onDismiss);
                    picker.Dispose();
                }
            }
        }

        private void RemoveOrientationChangeObserverAndNotifications()
        {
            if (viewController == null)
            {
                return;
            }

            UIDevice.CurrentDevice.EndGeneratingDeviceOrientationNotifications();
            NSNotificationCenter.DefaultCenter.RemoveObserver(observer);
            observer.Dispose();
        }

        private void DidRotate(NSNotification notice)
        {
            var device = (UIDevice)notice.Object;
            if (!IsValidInterfaceOrientation(device.Orientation) || Popover == null)
                return;
            if (orientation.HasValue && IsSameOrientationKind(orientation.Value, device.Orientation))
                return;

            if (UIDevice.CurrentDevice.CheckSystemVersion(6, 0))
            {
                if (!GetShouldRotate6(device.Orientation))
                    return;
            }
            else if (!GetShouldRotate(device.Orientation))
                return;

            var co = orientation;
            orientation = device.Orientation;

            if (co == null)
                return;

            DisplayPopover(hideFirst: true);
        }

        private bool GetShouldRotate(UIDeviceOrientation orientation)
        {
            var iorientation = UIInterfaceOrientation.Portrait;
            switch (orientation)
            {
                case UIDeviceOrientation.LandscapeLeft:
                    iorientation = UIInterfaceOrientation.LandscapeLeft;
                    break;

                case UIDeviceOrientation.LandscapeRight:
                    iorientation = UIInterfaceOrientation.LandscapeRight;
                    break;

                case UIDeviceOrientation.Portrait:
                    iorientation = UIInterfaceOrientation.Portrait;
                    break;

                case UIDeviceOrientation.PortraitUpsideDown:
                    iorientation = UIInterfaceOrientation.PortraitUpsideDown;
                    break;

                default: return false;
            }

            return viewController.ShouldAutorotateToInterfaceOrientation(iorientation);
        }

        private bool GetShouldRotate6(UIDeviceOrientation orientation)
        {
            if (!viewController.ShouldAutorotate())
                return false;

            var mask = UIInterfaceOrientationMask.Portrait;
            switch (orientation)
            {
                case UIDeviceOrientation.LandscapeLeft:
                    mask = UIInterfaceOrientationMask.LandscapeLeft;
                    break;

                case UIDeviceOrientation.LandscapeRight:
                    mask = UIInterfaceOrientationMask.LandscapeRight;
                    break;

                case UIDeviceOrientation.Portrait:
                    mask = UIInterfaceOrientationMask.Portrait;
                    break;

                case UIDeviceOrientation.PortraitUpsideDown:
                    mask = UIInterfaceOrientationMask.PortraitUpsideDown;
                    break;

                default: return false;
            }

            return viewController.GetSupportedInterfaceOrientations().HasFlag(mask);
        }

        private async Task<MediaFile> GetPictureMediaFile(NSDictionary info)
        {
            var image = (UIImage)info[UIImagePickerController.EditedImage] ?? (UIImage)info[UIImagePickerController.OriginalImage];

            var meta = info[UIImagePickerController.MediaMetadata] as NSDictionary;


            var path = GetOutputPath(MediaImplementation.TypeImage,
                options.Directory ?? ((IsCaptured) ? string.Empty : "temp"),
                options.Name);

            var cgImage = image.CGImage;

            if (options.PhotoSize != PhotoSize.Full)
            {
                try
                {
                    var percent = 1.0f;
                    switch (options.PhotoSize)
                    {
                        case PhotoSize.Large:
                            percent = .75f;
                            break;
                        case PhotoSize.Medium:
                            percent = .5f;
                            break;
                        case PhotoSize.Small:
                            percent = .25f;
                            break;
                        case PhotoSize.Custom:
                            percent = (float)options.CustomPhotoSize / 100f;
                            break;
                    }

                    //calculate new size
                    var width = (image.CGImage.Width * percent);
                    var height = (image.CGImage.Height * percent);

                    //begin resizing image
                    image = image.ResizeImageWithAspectRatio(width, height);

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unable to compress image: {ex}");
                }
            }

            //iOS quality is 0.0-1.0
            var quality = (options.CompressionQuality / 100f);
            image.AsJPEG(quality).Save(path, true);

            string aPath = null;
            if (source != UIImagePickerControllerSourceType.Camera)
            {

                //try to get the album path's url
                var url = (NSUrl)info[UIImagePickerController.ReferenceUrl];
                aPath = url?.AbsoluteString;
            }
            else
            {
                if (!options.SaveToAlbum)
                {
                    return new MediaFile(path, () => File.OpenRead(path), albumPath: aPath);
                }

                try
                {
                    var library = new ALAssetsLibrary();
                    var albumSave = await library.WriteImageToSavedPhotosAlbumAsync(cgImage, meta);
                    aPath = albumSave.AbsoluteString;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("unable to save to album:" + ex);
                }
            }

            return new MediaFile(path, () => File.OpenRead(path), albumPath: aPath);
        }



        private async Task<MediaFile> GetMovieMediaFile(NSDictionary info)
        {
            var url = (NSUrl)info[UIImagePickerController.MediaURL];

            var path = GetOutputPath(MediaImplementation.TypeMovie,
                      options.Directory ?? ((IsCaptured) ? string.Empty : "temp"),
                      options.Name ?? Path.GetFileName(url.Path));

            File.Move(url.Path, path);

            string aPath = null;
            if (source != UIImagePickerControllerSourceType.Camera)
            {
                //try to get the album path's url
                var url2 = (NSUrl)info[UIImagePickerController.ReferenceUrl];
                aPath = url2?.AbsoluteString;
            }
            else
            {
                if (!options.SaveToAlbum)
                {
                    return new MediaFile(path, () => File.OpenRead(path), albumPath: aPath);
                }

                try
                {
                    var library = new ALAssetsLibrary();
                    var albumSave = await library.WriteVideoToSavedPhotosAlbumAsync(new NSUrl(path));
                    aPath = albumSave.AbsoluteString;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("unable to save to album:" + ex);
                }
            }

            return new MediaFile(path, () => File.OpenRead(path), albumPath: aPath);
        }

        private static string GetUniquePath(string type, string path, string name)
        {
            var isPhoto = (type == MediaImplementation.TypeImage);
            var ext = Path.GetExtension(name);
            if (ext == string.Empty)
                ext = ((isPhoto) ? ".jpg" : ".mp4");

            name = Path.GetFileNameWithoutExtension(name);

            var nname = name + ext;
            var i = 1;
            while (File.Exists(Path.Combine(path, nname)))
                nname = name + "_" + (i++) + ext;

            return Path.Combine(path, nname);
        }

        private static string GetOutputPath(string type, string path, string name)
        {
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), path);
            Directory.CreateDirectory(path);

            if (!string.IsNullOrWhiteSpace(name))
            {
                return Path.Combine(path, GetUniquePath(type, path, name));
            }

            var timestamp = DateTime.Now.ToString("yyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            if (type == MediaImplementation.TypeImage)
                name = "IMG_" + timestamp + ".jpg";
            else
                name = "VID_" + timestamp + ".mp4";

            return Path.Combine(path, GetUniquePath(type, path, name));
        }

        private static bool IsValidInterfaceOrientation(UIDeviceOrientation self)
        {
            return (self != UIDeviceOrientation.FaceUp && self != UIDeviceOrientation.FaceDown && self != UIDeviceOrientation.Unknown);
        }

        private static bool IsSameOrientationKind(UIDeviceOrientation o1, UIDeviceOrientation o2)
        {
            if (o1 == UIDeviceOrientation.FaceDown || o1 == UIDeviceOrientation.FaceUp)
                return (o2 == UIDeviceOrientation.FaceDown || o2 == UIDeviceOrientation.FaceUp);
            if (o1 == UIDeviceOrientation.LandscapeLeft || o1 == UIDeviceOrientation.LandscapeRight)
                return (o2 == UIDeviceOrientation.LandscapeLeft || o2 == UIDeviceOrientation.LandscapeRight);
            if (o1 == UIDeviceOrientation.Portrait || o1 == UIDeviceOrientation.PortraitUpsideDown)
                return (o2 == UIDeviceOrientation.Portrait || o2 == UIDeviceOrientation.PortraitUpsideDown);

            return false;
        }

        private static UIDeviceOrientation GetDeviceOrientation(UIInterfaceOrientation self)
        {
            switch (self)
            {
                case UIInterfaceOrientation.LandscapeLeft:
                    return UIDeviceOrientation.LandscapeLeft;
                case UIInterfaceOrientation.LandscapeRight:
                    return UIDeviceOrientation.LandscapeRight;
                case UIInterfaceOrientation.Portrait:
                    return UIDeviceOrientation.Portrait;
                case UIInterfaceOrientation.PortraitUpsideDown:
                    return UIDeviceOrientation.PortraitUpsideDown;
                default:
                    throw new InvalidOperationException();
            }
        }
    }
}
