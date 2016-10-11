using Microsoft.ProjectOxford.Face;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace IotPrototype.EmotionRecognittion
{
    public class EmotionApi
    {

        private static readonly Lazy<EmotionApi> _recognizer = new Lazy<EmotionApi>(() => new EmotionApi());

        public static EmotionApi Instance
        {
            get
            {
                return _recognizer.Value;
            }
        }

        Microsoft.ProjectOxford.Emotion.EmotionServiceClient _emotionClient = new Microsoft.ProjectOxford.Emotion.EmotionServiceClient(GeneralConstants.EmpitionApiKey);
        
        async public Task<Microsoft.ProjectOxford.Emotion.Contract.Emotion[]> RecognizeEmotions(StorageFile imageFile, IEnumerable<Microsoft.ProjectOxford.Common.Rectangle> faceRectangles)
        {
            using (var fileStream = await imageFile.OpenStreamForReadAsync())
            {
                return await _emotionClient.RecognizeAsync(fileStream, faceRectangles.ToArray());
            }
        }


    }
}
