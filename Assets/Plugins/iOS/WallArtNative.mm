// Native iOS helpers for XR Wall Art:
//  - a photo picker so users can hang their own pictures
//  - light haptic feedback
//
// The picker is PHPickerViewController (iOS 14+). It runs out of process, so the app needs no
// photo-library permission and no usage description in Info.plist: the user simply picks one image.
// The chosen image is orientation-corrected, downscaled and written to a temporary JPEG, and
// its path is sent back to Unity with UnitySendMessage (an empty string means "cancelled").

#import <UIKit/UIKit.h>
#import <PhotosUI/PhotosUI.h>

extern UIViewController* UnityGetGLViewController(void);
extern void UnitySendMessage(const char* obj, const char* method, const char* msg);

@interface WallArtPhotoPicker : NSObject <PHPickerViewControllerDelegate, UIAdaptivePresentationControllerDelegate>
@property(nonatomic, copy) NSString* gameObjectName;
@property(nonatomic, copy) NSString* callbackName;
@property(nonatomic, assign) int maxPixelSize;
@property(nonatomic, assign) BOOL finished;
@end

static WallArtPhotoPicker* s_ActivePicker = nil;

@implementation WallArtPhotoPicker

- (void)present
{
    PHPickerConfiguration* config = [[PHPickerConfiguration alloc] init];
    config.filter = [PHPickerFilter imagesFilter];
    config.selectionLimit = 1;

    PHPickerViewController* picker = [[PHPickerViewController alloc] initWithConfiguration:config];
    picker.delegate = self;
    picker.modalPresentationStyle = UIModalPresentationPageSheet;
    picker.presentationController.delegate = self;

    UIViewController* root = UnityGetGLViewController();
    if (root == nil)
    {
        [self finishWithResult:@"error:no view controller"];
        return;
    }
    [root presentViewController:picker animated:YES completion:nil];
}

- (void)finishWithResult:(NSString*)result
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (self.finished) return;
        self.finished = YES;
        UnitySendMessage([self.gameObjectName UTF8String], [self.callbackName UTF8String], [result UTF8String]);
        if (s_ActivePicker == self) s_ActivePicker = nil;
    });
}

// Swiped the sheet away
- (void)presentationControllerDidDismiss:(UIPresentationController*)presentationController
{
    [self finishWithResult:@""];
}

- (void)picker:(PHPickerViewController*)picker didFinishPicking:(NSArray<PHPickerResult*>*)results
{
    [picker dismissViewControllerAnimated:YES completion:nil];

    if (results.count == 0)
    {
        [self finishWithResult:@""];
        return;
    }

    NSItemProvider* provider = results.firstObject.itemProvider;
    if ([provider canLoadObjectOfClass:[UIImage class]])
    {
        [provider loadObjectOfClass:[UIImage class] completionHandler:^(id<NSItemProviderReading> object, NSError* error) {
            UIImage* image = [(NSObject*)object isKindOfClass:[UIImage class]] ? (UIImage*)object : nil;
            [self handleImage:image error:error];
        }];
    }
    else if ([provider hasItemConformingToTypeIdentifier:@"public.image"])
    {
        [provider loadDataRepresentationForTypeIdentifier:@"public.image" completionHandler:^(NSData* data, NSError* error) {
            UIImage* image = data != nil ? [UIImage imageWithData:data] : nil;
            [self handleImage:image error:error];
        }];
    }
    else
    {
        [self finishWithResult:@"error:That item is not an image"];
    }
}

- (void)handleImage:(UIImage*)image error:(NSError*)error
{
    if (image == nil)
    {
        NSString* reason = error != nil ? error.localizedDescription : @"The image could not be loaded";
        [self finishWithResult:[@"error:" stringByAppendingString:reason]];
        return;
    }

    // Pixel size after applying the EXIF orientation
    CGFloat width = image.size.width * image.scale;
    CGFloat height = image.size.height * image.scale;
    if (width < 1 || height < 1)
    {
        [self finishWithResult:@"error:The image is empty"];
        return;
    }

    CGFloat longest = MAX(width, height);
    CGFloat factor = (self.maxPixelSize > 0 && longest > self.maxPixelSize) ? self.maxPixelSize / longest : 1.0;
    CGSize target = CGSizeMake(MAX(1, floor(width * factor)), MAX(1, floor(height * factor)));

    // Redrawing bakes the orientation into the pixels, so Unity does not have to read EXIF
    UIGraphicsImageRendererFormat* format = [UIGraphicsImageRendererFormat defaultFormat];
    format.scale = 1;
    format.opaque = YES;
    UIGraphicsImageRenderer* renderer = [[UIGraphicsImageRenderer alloc] initWithSize:target format:format];
    UIImage* normalized = [renderer imageWithActions:^(UIGraphicsImageRendererContext* context) {
        [[UIColor whiteColor] setFill];
        [context fillRect:CGRectMake(0, 0, target.width, target.height)];
        [image drawInRect:CGRectMake(0, 0, target.width, target.height)];
    }];

    NSData* jpeg = UIImageJPEGRepresentation(normalized, 0.9);
    if (jpeg == nil)
    {
        [self finishWithResult:@"error:The image could not be converted"];
        return;
    }

    NSString* fileName = [NSString stringWithFormat:@"wallart_pick_%@.jpg", [[NSUUID UUID] UUIDString]];
    NSString* path = [NSTemporaryDirectory() stringByAppendingPathComponent:fileName];
    if (![jpeg writeToFile:path atomically:YES])
    {
        [self finishWithResult:@"error:The image could not be saved"];
        return;
    }

    [self finishWithResult:path];
}

@end

extern "C" {

void WallArt_PickImage(const char* gameObjectName, const char* callbackName, int maxPixelSize)
{
    NSString* gameObject = [NSString stringWithUTF8String:gameObjectName ? gameObjectName : ""];
    NSString* callback = [NSString stringWithUTF8String:callbackName ? callbackName : ""];

    dispatch_async(dispatch_get_main_queue(), ^{
        if (s_ActivePicker != nil) return; // a picker is already open

        WallArtPhotoPicker* picker = [[WallArtPhotoPicker alloc] init];
        picker.gameObjectName = gameObject;
        picker.callbackName = callback;
        picker.maxPixelSize = maxPixelSize;
        s_ActivePicker = picker; // keeps the delegate alive while the sheet is up
        [picker present];
    });
}

// style: 0 = light tap, 1 = medium tap, 2 = success notification
void WallArt_Haptic(int style)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (style == 2)
        {
            UINotificationFeedbackGenerator* generator = [[UINotificationFeedbackGenerator alloc] init];
            [generator notificationOccurred:UINotificationFeedbackTypeSuccess];
        }
        else
        {
            UIImpactFeedbackStyle impact = style == 1 ? UIImpactFeedbackStyleMedium : UIImpactFeedbackStyleLight;
            UIImpactFeedbackGenerator* generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:impact];
            [generator impactOccurred];
        }
    });
}

}
