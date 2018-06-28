using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace PriceTagOCR
{
    public class PriceTagProcessor
    {
        private double _probabilityThreshold = 0.5;
        private string[] _detectionLabels = new string[] { "PriceTag" };
        private string _customVisionPredictionKey;
        private Guid _customVisionProjectId;
        private string _computerVisionSubscriptionKey;
        private const string _computerVisionBaseUri = "https://westeurope.api.cognitive.microsoft.com/vision/v2.0/recognizeText";   // MODIFY to match region where you created Computer Vision instance


        /// <summary>
        /// Constructor - initializing the settings for Cognitive Services
        /// </summary>
        /// <param name="customVisionPredictionKey"></param>
        /// <param name="customVisionProjectId"></param>
        /// <param name="computerVisionSubscriptionKey"></param>
        public PriceTagProcessor(string customVisionPredictionKey, string customVisionProjectId, string computerVisionSubscriptionKey)
        {
            this._customVisionPredictionKey = customVisionPredictionKey;
            this._customVisionProjectId = new Guid(customVisionProjectId);
            this._computerVisionSubscriptionKey = computerVisionSubscriptionKey;
        }

        /// <summary>
        /// Process an image: perform object detection for price tags, crop each detected object, perform OCR on each object.
        /// OCR is performed on a cropped image to improve accuracy of the OCRing.
        /// </summary>
        /// <param name="imagePath"></param>
        /// <param name="saveLocation"></param>
        /// <returns></returns>
        public async Task ProcessImage(string imagePath, string saveLocation)
        {
            using (var stream = File.OpenRead(imagePath))
            {
                Console.WriteLine($"Processing file {imagePath}...");
                Console.WriteLine("\tDetecting price tags...");

                // Create a Custom Vision prediction endpoint
                PredictionEndpoint endpoint = new PredictionEndpoint() { ApiKey = this._customVisionPredictionKey };

                // Invoke the prediction endpoint to detect price tags
                var result = endpoint.PredictImage(this._customVisionProjectId, stream);

                int tagCount = 0;

                Console.WriteLine($"\tDetected {result.Predictions.Count} tags:");

                // Loop over each prediction and write out the results
                foreach (var c in result.Predictions)
                {
                    // Filter the detected objects to be of the right class and high enough probability
                    if (_detectionLabels.Contains(c.TagName) && c.Probability > this._probabilityThreshold)
                    {
                        // Recalculate bounding boxes from normalized values [0;1]
                        Image sourceImage = Image.FromFile(imagePath);
                        Rectangle box = CalculateBoundingBox(c.BoundingBox, sourceImage.Width, sourceImage.Height);

                        // Crop image to the recalculated bounded box
                        var cropped = CropImage(sourceImage, box);

                        Console.WriteLine($"\t\t{c.TagName}: {c.Probability:P1} [ {box.Left}, {box.Top}, {box.Width}, {box.Height} ]");

                        // Save the extracted objects to disk
                        cropped.Save(Path.Combine(saveLocation, $"tag_{tagCount}.jpg"));

                        Console.WriteLine("\t\tPerforming OCR...");

                        // Perform OCR on the detected price tags
                        await ReadText(cropped);

                        // Increment the counter
                        tagCount++;
                    }
                }
            }
        }

        /// <summary>
        /// Recalculate the bounding box because sometimes the actual price tag is not entirely enclosed by the bounding box provided by the object detection algorithm.
        /// This could be improved by adding more images to our training sample.
        /// Concretely, 'recalculation' consists of denormalizing the coordinates (originally in a [0-1] range) and by doubling the size of the bounding box.
        /// </summary>
        /// <param name="detectedBoundingBox"></param>
        /// <param name="imageWidth"></param>
        /// <param name="imageHeight"></param>
        /// <returns></returns>
        private static Rectangle CalculateBoundingBox(BoundingBox detectedBoundingBox, int imageWidth, int imageHeight)
        {
            // Denormalize the bounding box dimensions [0;1]
            int left = Convert.ToInt32(detectedBoundingBox.Left * imageWidth);
            int top = Convert.ToInt32(detectedBoundingBox.Top * imageHeight);
            int width = Convert.ToInt32(detectedBoundingBox.Width * imageWidth);
            int height = Convert.ToInt32(detectedBoundingBox.Height * imageHeight);


            // Increase the bounding box size to cover for missing parts
            left = Math.Max(0, left - (width / 2));
            width = Math.Min(2 * width, imageWidth - left);
            top = Math.Max(0, top - (height / 2));
            height = Math.Min(2 * height, imageHeight - top);

            Rectangle box = new Rectangle(left, top, width, height);
            return box;
        }

        /// <summary>
        /// Crop the image to the desired size
        /// </summary>
        /// <param name="originalImage"></param>
        /// <param name="sourceRectangle"></param>
        /// <param name="destinationRectangle"></param>
        /// <returns></returns>
        static Bitmap CropImage(Image originalImage, Rectangle sourceRectangle, Rectangle? destinationRectangle = null)
        {
            if (destinationRectangle == null)
            {
                destinationRectangle = new Rectangle(Point.Empty, sourceRectangle.Size);
            }

            var croppedImage = new Bitmap(destinationRectangle.Value.Width,
                destinationRectangle.Value.Height);
            using (var graphics = Graphics.FromImage(croppedImage))
            {
                graphics.DrawImage(originalImage, destinationRectangle.Value,
                    sourceRectangle, GraphicsUnit.Pixel);
            }
            return croppedImage;
        }

        /// <summary>
        /// Gets the text from the specified image file by using the Computer Vision REST API.
        /// </summary>
        /// <param name="imageFilePath">The image file with text.</param>
        private async Task ReadText(Bitmap image)
        {
            try
            {
                HttpClient client = new HttpClient();

                // Request headers.
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", this._computerVisionSubscriptionKey);

                // Request parameter.
                // Note: The request parameter changed for APIv2.
                // For APIv1, it is "handwriting=true".
                string requestParameters = "mode=Handwritten";

                // Assemble the URI for the REST API Call.
                string uri = _computerVisionBaseUri + "?" + requestParameters;

                HttpResponseMessage response;

                // Two REST API calls are required to extract handwritten text.
                // One call to submit the image for processing, the other call
                // to retrieve the text found in the image.
                // operationLocation stores the REST API location to call to
                // retrieve the text.
                string operationLocation;

                // Request body.
                // Posts a locally stored JPEG image.
                byte[] byteData = ImageToByte(image);

                using (ByteArrayContent content = new ByteArrayContent(byteData))
                {
                    // This example uses content type "application/octet-stream".
                    // The other content types you can use are "application/json"
                    // and "multipart/form-data".
                    content.Headers.ContentType =
                        new MediaTypeHeaderValue("application/octet-stream");

                    // The first REST call starts the async process to analyze the
                    // written text in the image.
                    response = await client.PostAsync(uri, content);
                }

                // The response contains the URI to retrieve the result of the process.
                if (response.IsSuccessStatusCode)
                    operationLocation =
                        response.Headers.GetValues("Operation-Location").FirstOrDefault();
                else
                {
                    // Display the JSON error data.
                    string errorString = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("\n\nResponse:\n{0}\n",
                        JToken.Parse(errorString).ToString());
                    return;
                }

                // The second REST call retrieves the text written in the image.
                //
                // Note: The response may not be immediately available. Handwriting
                // recognition is an async operation that can take a variable amount
                // of time depending on the length of the handwritten text. You may
                // need to wait or retry this operation.
                //
                // This example checks once per second for ten seconds.
                string contentString;
                int i = 0;
                do
                {
                    System.Threading.Thread.Sleep(1000);
                    response = await client.GetAsync(operationLocation);
                    contentString = await response.Content.ReadAsStringAsync();
                    ++i;
                }
                while (i < 10 && contentString.IndexOf("\"status\":\"Succeeded\"") == -1);

                if (i == 10 && contentString.IndexOf("\"status\":\"Succeeded\"") == -1)
                {
                    Console.WriteLine("\nTimeout error.\n");
                    return;
                }

                var json = JToken.Parse(contentString);
                //json.SelectTokens("");


                // Display the text response.
                var tokens = json.SelectTokens("$..lines[*].text");
                foreach (var token in tokens)
                {
                    Console.WriteLine($"\t\tText: {token.Value<string>()}");

                    // TODO: parse the resulting text to determine the product SKU and price
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("\n" + e.Message);
            }
        }

        /// <summary>
        /// Convert an Image object into a byte array
        /// </summary>
        /// <param name="img"></param>
        /// <returns></returns>
        private byte[] ImageToByte(Image img)
        {
            ImageConverter converter = new ImageConverter();
            return (byte[])converter.ConvertTo(img, typeof(byte[]));
        }


    }
}
