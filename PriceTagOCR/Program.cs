// OCR on image: https://docs.microsoft.com/en-us/azure/cognitive-services/Computer-vision/quickstarts/csharp-print-text
// Computer vision API: https://westus.dev.cognitive.microsoft.com/docs/services/5adf991815e1060e6355ad44/operations/587f2c6a154055056008f200
// Object detection using Custom Vision: https://docs.microsoft.com/en-us/azure/cognitive-services/custom-vision-service/csharp-tutorial-od


namespace PriceTagOCR
{
    class Program
    {
        /// <summary>
        /// Use object detection to find price tags, crop the image to the bounding box and then perform OCR on each price tag.
        /// Command line arguments:
        ///   source image
        ///   target folder
        ///   custom vision prediction key
        ///   custom vision project id
        ///   computer vision subscription key
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        static int Main(string[] args)
        {
            // Parse command line arguments
            if (args.Length != 5)
            {
                System.Console.WriteLine("Usage: PriceTagOCR.exe <image path> <target folder> <custom vision prediction key> <custom vision project ID> <computer vision subscription key>");
                return 1;
            }

            string imagePath = args[0];
            string targetFolder = args[1];
            string customVisionPredictionKey = args[2];
            string customVisionProjectId = args[3];
            string computerVisionSubscriptionKey = args[4];


            // Process the input image
            PriceTagProcessor processor = new PriceTagProcessor(customVisionPredictionKey, customVisionProjectId, computerVisionSubscriptionKey);
            processor.ProcessImage(imagePath, targetFolder).Wait();

            return 0;
        }
    }
}
