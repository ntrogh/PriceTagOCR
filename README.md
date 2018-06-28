# Price tag OCR

Project to allow extracting the price tags from a picture in a store. In a second step, the images of the price tags are then OCRed.

Example picture:

![Sample store image](./images/store_sample_small.jpg)

The project makes use of the following Microsoft Cognitive Services:

* [Custom Vision - Object Detection](https://docs.microsoft.com/en-us/azure/cognitive-services/custom-vision-service/csharp-tutorial-od)
* [Computer Vision](https://docs.microsoft.com/en-us/azure/cognitive-services/Computer-vision/quickstarts/csharp-print-text)

## Prerequisites

### Custom Vision Object Detection

To retrieve the information from the price tags, we will first be using object detection to extract the price tags from the source image. In the next step we will then perform OCR on those price tags. For the object detection, we're using [Microsoft Cognitive Services Custom Vision](https://docs.microsoft.com/en-us/azure/cognitive-services/Custom-Vision-Service/home), which has object detection capabilities, currently in preview (as of writing).

1. Sign into the [Custom Vision portal](https://customvision.ai/)
2. Create a new project - make sure to select the **Limited trial** resource group and **Object Detection (preview)** project type
3. Add at least 15 images that contain price tags (see the example image above)
4. Add a new tag and name it **PriceTag**
5. For each image, draw bounding boxes around each price tag and select the **PriceTag** tag to label it

    ![Custom Vision object tagging](./images/custom_vision_tagging.png)

6. Train the Custom Vision model
7. Take note of the **Project Id** and **Prediction Key** in the project settings screen

### Cognitive Services Computer Vision

Once we have detected the price tags, we will perform OCR on the extracted price tags. For this, we will use the [Microsoft Cognitive Services Computer Vision API](https://azure.microsoft.com/en-us/try/cognitive-services/my-apis/?api=computer-vision).

1. Open the [Azure Management portal](https://portal.azure.com)
2. Click the '+" button to create a new resource
3. Search for **Computer Vision** and create a new instance
4. Note down the Computer Vision subscription key

>> **Note**: when you choose another location than *West Europe*, you will need to change the computer vision base Uri in the source code to point to your selected region

```csharp
private const string _computerVisionBaseUri = "https://westeurope.api.cognitive.microsoft.com/vision/v2.0/recognizeText";
```

## Running the project

The console application takes a number of command-line arguments. These can be provided when running the executable or can be configured in the Visual Studio Project Settings: Project > PriceTagOCR Properties > Debug > Comand line arguments

Command-line arguments:

1. path to source image
2. output folder where the extracted price tag images are stored
3. Custom Vision prediction key
4. Custom Vision project Id
5. Computer Vision subscription key
