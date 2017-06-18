using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Management;
using System.Web.WebSockets;
using HtmlAgilityPack;
using Iveonik.Stemmers;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Complex;
using MathNet.Numerics.Random;

namespace LSI
{
    class Program
    {
        private static string[] _bannedPrefixes = {"/", "<", "href", "alt", "disabled", "id", "src", "style", "title", "class"};
        private static char[] _punctuationMarks = 
        {
            '=', '.', ',', '?', '!', '(', ')', ':', 
            '-', '<', '>', '[', ']', '"', '\'', 
            '/', ';', '|', '&', '%', '#', '*', '+', 
            '0','1', '2', '3', '4', '@', '_',
            '5', '6', '7', '8', '9', '\n', '\r', '\t' 
        };

        public class DocumentWordsCountModel
        {
            public DocumentWordsCountModel()
            {
                WordsCount = new Dictionary<string, int>();
            }
            public Dictionary<string, int> WordsCount { get; set; }
            public string FileName { get; set; }
        }


        static void Main(string[] args)
        {
            var rnd = GetRandomMatrix(3, 5);
            var matrix = new[,] {{1.0, 1.0, 1.0}, {1.0, 1.0, 2.0}};
            NormalizeRows(matrix, 2, 3);
            var path = @"C:\Users\mareczek\Source\Repos\plsa\Sample documents";
            //var path = @"C:\Users\mareczek\PycharmProjects\cache_cam_html";
            var allWords = new HashSet<string>();
            var stemmer = new EnglishStemmer();
            var documentsWordsCount = new List<DocumentWordsCountModel>();
            var documentsCount = 20;
            var htmlDocuments = new List<string>();
            foreach (var file in new DirectoryInfo(path).GetFiles())
            {
                using (var fs = file.OpenRead())
                {
                    Console.WriteLine(file.Name);
                    var htmlDocument = new HtmlDocument();
                    htmlDocument.Load(fs);
                    htmlDocuments.Add(file.Name);
                    var title = htmlDocument.DocumentNode.SelectSingleNode("//title");
                    var body = htmlDocument.DocumentNode.SelectSingleNode("//body");
                    if (body == null)
                    {
                        continue;
                    }
                    
                    var docCountModel = new DocumentWordsCountModel(){ FileName = file.Name };
                    

                    var bodyContent = body.InnerText;
                    var words = bodyContent.Split(' ');
                    if (title != null)
                    {
                        words = words.Union(title.InnerText.Split(' ', '\n', '\r', '\t')).ToArray();
                    }
                    
                    foreach (var word in words)
                    {
                        if (StartsWithPrefix(word))
                        {
                            continue;
                        }
                        var lowercase = HttpUtility.HtmlDecode(word).ToLower();
                        var trimmed = lowercase.Trim(_punctuationMarks);
                        if (_punctuationMarks.Any(x => trimmed.Contains(x)))
                        {
                            continue;
                        }
                        if (trimmed.Length < 2)
                        {
                            continue;
                        }
                        var stemmed = stemmer.Stem(trimmed);
                        if (!allWords.Contains(stemmed))
                        {
                            allWords.Add(stemmed);
                        }
                        if (docCountModel.WordsCount.ContainsKey(stemmed))
                        {
                            docCountModel.WordsCount[stemmed]++;
                        }
                        else
                        {
                            docCountModel.WordsCount.Add(stemmed, 1);
                        }
                    }

                    documentsWordsCount.Add(docCountModel);
                    if (documentsWordsCount.Count > documentsCount)
                    {
                        break;
                    }
                }
            }

            

            var allWordsCount = allWords.Count;
            var allDocumentsCount = documentsWordsCount.Count;
            var topicsCount = 2;

            var allWordsArray = allWords.ToArray();
            var wordsCountArray = documentsWordsCount.ToArray();
            var X = new double[allWordsCount, allDocumentsCount];
            for (var x = 0; x < allWordsArray.Length; x++)
            {
                for (var y = 0; y < wordsCountArray.Length; y++)
                {
                    if (documentsWordsCount[y].WordsCount.ContainsKey(allWordsArray[x]))
                    {
                        X[x, y] = documentsWordsCount[y].WordsCount[allWordsArray[x]];
                    }
                    else
                    {
                        X[x, y] = 0.0;
                    }
                }
            }

            var v = Vector<double>.Build.DenseOfArray(new double[] { 0, 3 });
            var v2 = Vector<double>.Build.DenseOfArray(new double[] { 0, 3 });
            var test = cosSimilarity(v, v2);

            var x2 = new double[3, 4]
            {
                {1, 1, 0, 0},
                {0, 0, 1, 1},
                {0, 0, 0, 1}
            };

            //plsa_bardziej_czytelne(x2, 2);
            //plsa2(x2, 2);

            plsa_bardziej_czytelne(TransposeRowsAndColumns(X), 5);
                
            plsa2(TransposeRowsAndColumns(X), 5, 1000);
            plsa(TransposeRowsAndColumns(X), 5, 1000);
            Console.ReadLine();


            var P1 = GetRandomMatrix(allWordsCount, topicsCount);// new double[allWordsCount, topicsCount];
            var P2 = GetRandomMatrix(topicsCount, allDocumentsCount);// new double[topicsCount, allDocumentsCount];
            int sfgdgf = 10;
            while (--sfgdgf>0)
            {
                var avgDif = 0.0;
                for (var t = 0; t < allWordsCount; t++)
                {
                    for (var k = 0; k < topicsCount; k++)
                    {
                        var sum = 0.0;
                        for (var d = 0; d < allDocumentsCount; d++)
                        {
                            var nom = X[t, d];
                            var den = 0.0;
                            for (var nextK = 0; nextK < topicsCount; nextK++)
                            {
                                den += P1[t, nextK]*P2[nextK, d];
                            }
                            var frac = nom/den;
                            frac = frac*P2[k, d];
                            sum += frac;
                        }
                        if (P1[t, k] != 0.0)
                        {
                            var dif = Math.Abs(P1[t, k] * sum - P1[t, k]) / P1[t, k];
                            avgDif += dif;
                        }
                        P1[t, k] = P1[t, k]*sum;

                    }
                }
                NormalizeRows(P1, allWordsCount, topicsCount);

                for (var k = 0; k < topicsCount; k++)
                {
                    for (var d = 0; d < allDocumentsCount; d++)
                    {
                        var sum = 0.0;
                        for (var t = 0; t < allWordsCount; t++)
                        {
                            var nom = X[t, d];
                            var den = 0.0;
                            for (var nextK = 0; nextK < topicsCount; nextK++)
                            {
                                den += P1[t, nextK]*P2[nextK, d];
                            }
                            if (den != 0.0)
                            {
                                var frac = nom/den;
                                frac = frac*P1[t, k];
                                sum += frac;
                            }
                        }
                        
                        if (P2[k, d] != 0.0)
                        {
                            var dif = Math.Abs(P2[k, d] - P2[k, d] * sum) / P2[k, d];

                            avgDif += dif;
                        }
                        P2[k, d] = P2[k, d] * sum;
                       
                    }
                }
            //    avgDif = avgDif/(topicsCount*documentsCount + allWordsCount*topicsCount);
                NormalizeRows(P2, topicsCount, allDocumentsCount);
            }

            var topics = new List<string>[topicsCount];
            var topicsDocuments = new List<string>[topicsCount];
            for (var i = 0; i < topicsCount; i++)
            {
                topics[i] = new List<string>();
                topicsDocuments[i] = new List<string>();
            }
            for (var w = 0; w < allWordsCount; w++)
            {
                var max = 0.0;
                var max_ind = 0;
                for (var col = 0; col < topicsCount; col++)
                {
                    if (P1[w, col] > max)
                    {
                        max = P1[w, col];
                        max_ind = col;
                    }
                }
                topics[max_ind].Add(allWordsArray[w]);
            }

           
            for (var i = 0; i < allDocumentsCount; i++)
            {
                var max = 0.0;
                var max_ind = 0;
                for (var t = 0; t < topicsCount; t++)
                {
                    if (P2[t, i] > max)
                    {
                        max = P2[t, i];
                        max_ind = t;
                    }
                }
                topicsDocuments[max_ind].Add(htmlDocuments[i]);
            }

            var a = 2;

           // var topicsWithDocuments = new 
            //var allWordsArray = allWords.ToArray();
            //var wordsCountArray = documentsWordsCount.ToArray();

            //var matrix = Matrix<double>.Build.Dense(allWords.Count, documentsWordsCount.Count, 0.0);
            //for (var x = 0; x < allWordsArray.Length; x++)
            //{
            //    for (var y = 0; y < wordsCountArray.Length; y++)
            //    {
            //        if (documentsWordsCount[y].WordsCount.ContainsKey(allWordsArray[x]))
            //        {
            //            matrix[x, y] = documentsWordsCount[y].WordsCount[allWordsArray[x]];
            //        }
            //    }
            //}
            //var svd = matrix.Svd();

        }

        private static double[,] GetRandomMatrix(int rows, int columns)
        {
            var rnd = new Random();
            var result = new double[rows, columns];
            for (var row = 0; row < rows; row++)
            {
                var sum = 0.0;
                for (var col = 0; col < columns; col++)
                {
                    var next = rnd.NextDouble();
                    sum += next;
                    result[row,col] = next;
                }
                for (var col = 0; col < columns; col++)
                {
                    result[row, col] = result[row, col]/sum;
                }
            }
            return result;
        }

        private static void NormalizeRows(double[,] matrix)
        {
            NormalizeRows(matrix, matrix.GetLength(0), matrix.GetLength(1));
        }

        private static void NormalizeRows(double[,] matrix, int rows, int columns)
        {
            for (var row = 0; row < rows; row++)
            {
                var rowSum = 0.0;
                for (var col = 0; col < columns; col++)
                {
                    rowSum += matrix[row, col];
                }
                if (rowSum != 0.0)
                {
                    for (var col = 0; col < columns; col++)
                    {
                        matrix[row, col] = matrix[row, col]/rowSum;
                    }
                }
            }
        }

        private static bool StartsWithPrefix(string word)
        {
            return _bannedPrefixes.Any(word.StartsWith);
        }

        public static void plsa(double[,] documentWordMatrix, int numberOfTopics, int maxIterations = 100)
        {
            int numberOfDocuments = documentWordMatrix.GetLength(0); //rows N
            int numberOfWords = documentWordMatrix.GetLength(1); //cols M

            var documentTopicMatrix = GetRandomMatrix(numberOfDocuments, numberOfTopics); //P(z | d)
            var topicWordMatrix = GetRandomMatrix(numberOfTopics, numberOfWords); // P(w | z)
            var topicMatrix = new double[numberOfDocuments, numberOfWords, numberOfTopics]; //γ P(z | d, w)

            NormalizeRows(documentTopicMatrix);
            NormalizeRows(topicWordMatrix);

            var tmp = 0.0;
            var lambdaMatrix = new double[1, numberOfTopics];
            for (int i = 0; i < numberOfTopics; i++)
            {
                lambdaMatrix[0, i] = 1;
            }
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                //stepE
                for (int i_document_index = 0; i_document_index < numberOfDocuments; i_document_index++)
                {
                    for (int j_word_index = 0; j_word_index < numberOfWords; j_word_index++)
                    {
                        double normalizeSum = 0.0;
                        for (int k_topic_index = 0; k_topic_index < numberOfTopics; k_topic_index++)
                        {
                            double prob = documentTopicMatrix[i_document_index, k_topic_index]*
                                          topicWordMatrix[k_topic_index, j_word_index];
                            normalizeSum += prob;
                            topicMatrix[i_document_index, j_word_index, k_topic_index] = prob;
                        }
                        //normalizacja
                        if (normalizeSum != 0.0)
                        {
                            for (int k_topic_index = 0; k_topic_index < numberOfTopics; k_topic_index++)
                            {
                                topicMatrix[i_document_index, j_word_index, k_topic_index] /= normalizeSum;
                            }
                        }
                    }
                }
                //stepM
                for (int k_topic_index = 0; k_topic_index < numberOfTopics; k_topic_index++)
                {
                    double normalizeSum = 0.0;
                    for (int j_word_index = 0; j_word_index < numberOfWords; j_word_index++)
                    {
                        double s = 0.0;
                        for (int i_document_index = 0; i_document_index < numberOfDocuments; i_document_index++)
                        {
                            double value = documentWordMatrix[i_document_index, j_word_index];
                            s = s + value*topicMatrix[i_document_index, j_word_index, k_topic_index];
                        }
                        topicWordMatrix[k_topic_index, j_word_index] = s;
                        normalizeSum += s;
                    }
                    //normalizacja
                    if (normalizeSum != 0.0)
                    {
                        for (int j_word_index = 0; j_word_index < numberOfWords; j_word_index++)
                        {
                            topicWordMatrix[k_topic_index, j_word_index] /= normalizeSum;
                        }
                    }
                }

                for (int i_document_index = 0; i_document_index < numberOfDocuments; i_document_index++)
                {
                    double normalizeSum = 0.0;
                    for (int k_topic_index = 0; k_topic_index < numberOfTopics; k_topic_index++)
                    {
                        double s = 0.0;
                        for (int j_word_index = 0; j_word_index < numberOfWords; j_word_index++)
                        {
                            double value = documentWordMatrix[i_document_index, j_word_index];
                            s = s + value*topicMatrix[i_document_index, j_word_index, k_topic_index];
                        }
                        documentTopicMatrix[i_document_index, k_topic_index] = s;
                        normalizeSum += s;
                    }
                    //normalizacja
                    if (normalizeSum != 0.0)
                    {
                        for (int k_topic_index = 0; k_topic_index < numberOfTopics; k_topic_index++)
                        {
                            documentTopicMatrix[i_document_index, k_topic_index] /= normalizeSum;
                        }
                    }
                }
                var l = likelihood(documentWordMatrix, numberOfTopics, documentTopicMatrix, lambdaMatrix, topicWordMatrix, numberOfDocuments, numberOfWords);
                if (Math.Abs(l - tmp) < 1.0e-10)
                {
                    Console.WriteLine(iteration);
                    break;
                }
                else
                {
                    tmp = l;
                }
            }
            
            printResults(documentWordMatrix, numberOfTopics, documentTopicMatrix, lambdaMatrix, topicWordMatrix, numberOfDocuments);
            Console.WriteLine("Done");
        }

        public static void plsa_bardziej_czytelne(double[,] matrix, int topics, int maxIterations = 1000)
        {
            int documents = matrix.GetLength(0); //rows N
            int words = matrix.GetLength(1); //cols M

            var docTop = Matrix<double>.Build.DenseOfArray(GetRandomMatrix(documents, topics)).NormalizeRows(1.0);
            var topWord = Matrix<double>.Build.DenseOfArray(GetRandomMatrix(topics, words)).NormalizeRows(1.0);
            var top = Matrix<double>.Build.DenseOfArray(GetRandomMatrix(1, topics)).Row(0).Normalize(1.0);
            var likelihoodMatrix = Matrix<double>.Build.Dense(documents, words);
            
            var docWordTop = Enumerable.Range(0, documents).Select(d => Matrix<double>.Build.Dense(words, topics)).ToList();
            var previous_L = 0.0;
            for (int iter = 0; iter < maxIterations; iter++)
            {
                // E
                // P(z|d,w)
                for (int d = 0; d < documents; d++)
                    for (int w = 0; w < words; w++)
                    {
                        for (int z = 0; z < topics; z++)
                            docWordTop[d][w, z] = top[z]*docTop[d, z]*topWord[z, w];
                        docWordTop[d].SetRow(w, docWordTop[d].Row(w).Normalize(1.0));
                    }
                // M
                // P(w|z)
                for (int z = 0; z < topics; z++)
                {
                    for (int w = 0; w < words; w++)
                        topWord[z, w] = Enumerable.Range(0, documents).Sum(d => matrix[d, w] * docWordTop[d][w, z]);
                    topWord.SetRow(z, topWord.Row(z).Normalize(1.0));
                }

                //P(d|z)
                for (int z = 0; z < topics; z++)
                {
                    for (int d = 0; d < documents; d++)
                        docTop[d, z] = Enumerable.Range(0, words).Sum(w => matrix[d, w]*docWordTop[d][w, z]);
                    docTop.SetColumn(z, docTop.Column(z).Normalize(1.0));
                }

                //P(z)
                for (int z = 0; z < topics; z++)
                    top[z] = Enumerable.Range(0, documents)
                        .Sum(d => Enumerable.Range(0, words).Sum(w => matrix[d, w] * docWordTop[d][w, z]));
                top = top.Normalize(1.0);

                //likelyhood
                for (int d = 0; d < documents; d++)
                    for (int w = 0; w < words; w++)
                        likelihoodMatrix[d, w] = Enumerable.Range(0, topics).Sum(z => top[z]*docTop[d, z]*topWord[z, w]);
                var likelihood = Enumerable.Range(0, documents)
                    .Sum(d => Enumerable.Range(0, words).Where(w => likelihoodMatrix[d, w] != 0).Sum(w => matrix[d, w]*Math.Log(likelihoodMatrix[d, w])));
                if (Math.Abs(likelihood - previous_L) < 1.0e-5) //mozna troche zwiekszyc ale trzeba uwazac bo pojawiają się NaN'y przez sumowanie / mnożenie małych liczb
                {
                    Console.WriteLine(iter);
                    break;
                }
                previous_L = likelihood;
            }
            var after = docTop.Multiply(Matrix<double>.Build.DenseOfDiagonalVector(top)).Multiply(topWord);
            printResults2(documents, Matrix<double>.Build.DenseOfArray(matrix), after);
            Console.WriteLine("Done");
        }

        public static void plsa2(double[,] documentWordMatrix, int numberOfTopics, int maxIterations = 100)
        {
            int numberOfDocuments = documentWordMatrix.GetLength(0); //rows N
            int numberOfWords = documentWordMatrix.GetLength(1); //cols M

            var documentTopicMatrix = GetRandomMatrix(numberOfDocuments, numberOfTopics); //P(z | d)
            var topicWordMatrix = GetRandomMatrix(numberOfTopics, numberOfWords); // P(w | z)
            var lambdaMatrix = GetRandomMatrix(1, numberOfTopics);
            var gammaMatrix = new double[numberOfDocuments, numberOfWords, numberOfTopics]; //γ P(z | d, w)

            NormalizeRows(documentTopicMatrix);
            NormalizeRows(topicWordMatrix);
            NormalizeRows(lambdaMatrix);

            var tmp = 0.0;
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                //stepE
                for (int i_document_index = 0; i_document_index < numberOfDocuments; i_document_index++)
                {
                    for (int j_word_index = 0; j_word_index < numberOfWords; j_word_index++)
                    {
                        double normalizeSum = 0.0;
                        for (int k_topic_index = 0; k_topic_index < numberOfTopics; k_topic_index++)
                        {
                            double prob = lambdaMatrix[0, k_topic_index] *
                                documentTopicMatrix[i_document_index, k_topic_index] *
                                   topicWordMatrix[k_topic_index, j_word_index];
                            normalizeSum += prob;
                            gammaMatrix[i_document_index, j_word_index, k_topic_index] = prob;
                        }
                        if (normalizeSum != 0.0)
                        {
                            //normalizacja
                            for (int k_topic_index = 0; k_topic_index < numberOfTopics; k_topic_index++)
                            {
                                gammaMatrix[i_document_index, j_word_index, k_topic_index] /= normalizeSum;
                            }
                        }
                    }
                }
                //stepM
                for (int k_topic_index = 0; k_topic_index < numberOfTopics; k_topic_index++)
                {
                    double s = 0.0;
                    for (int i_document_index = 0; i_document_index < numberOfDocuments; i_document_index++)
                    {
                        for (int j_word_index = 0; j_word_index < numberOfWords; j_word_index++)
                        {
                            double value = documentWordMatrix[i_document_index, j_word_index];
                            s = s + value*gammaMatrix[i_document_index, j_word_index, k_topic_index];
                        }
                    }
                    lambdaMatrix[0, k_topic_index] = s;
                }
                NormalizeRows(lambdaMatrix);            

                for (int k_topic_index = 0; k_topic_index < numberOfTopics; k_topic_index++)
                {
                    double normalizeSum = 0.0;
                    for (int j_word_index = 0; j_word_index < numberOfWords; j_word_index++)
                    {
                        double s = 0.0;
                        for (int i_document_index = 0; i_document_index < numberOfDocuments; i_document_index++)
                        {
                            double value = documentWordMatrix[i_document_index, j_word_index];
                            s = s + value * gammaMatrix[i_document_index, j_word_index, k_topic_index];
                        }
                        topicWordMatrix[k_topic_index, j_word_index] = s;
                        normalizeSum += s;
                    }
                    //normalizacja
                    if (normalizeSum != 0.0)
                    {
                        for (int j_word_index = 0; j_word_index < numberOfWords; j_word_index++)
                        {
                            topicWordMatrix[k_topic_index, j_word_index] /= normalizeSum;
                        }
                    }
                }

                for (int i_document_index = 0; i_document_index < numberOfDocuments; i_document_index++)
                {
                    double normalizeSum = 0.0;
                    for (int k_topic_index = 0; k_topic_index < numberOfTopics; k_topic_index++)
                    {
                        double s = 0.0;
                        for (int j_word_index = 0; j_word_index < numberOfWords; j_word_index++)
                        {
                            double value = documentWordMatrix[i_document_index, j_word_index];
                            s = s + value * gammaMatrix[i_document_index, j_word_index, k_topic_index];
                        }
                        documentTopicMatrix[i_document_index, k_topic_index] = s;
                        normalizeSum += s;
                    }
                    //normalizacja
                    if (normalizeSum != 0.0)
                    {
                        for (int k_topic_index = 0; k_topic_index < numberOfTopics; k_topic_index++)
                        {
                            documentTopicMatrix[i_document_index, k_topic_index] /= normalizeSum;
                        }
                    }
                }

                var l = likelihood(documentWordMatrix, numberOfTopics, documentTopicMatrix, lambdaMatrix, topicWordMatrix, numberOfDocuments, numberOfWords);
                if (Math.Abs(l - tmp) < 1.0e-10)
                {
                    Console.WriteLine(iteration);
                    break;
                }
                else
                {
                    tmp = l;
                }
            }
            printResults(documentWordMatrix, numberOfTopics, documentTopicMatrix, lambdaMatrix, topicWordMatrix, numberOfDocuments);
            Console.WriteLine("Done");
        }

        private static void printResults(double[,] documentWordMatrix, int numberOfTopics, double[,] documentTopicMatrix,
            double[,] lambdaMatrix, double[,] topicWordMatrix, int numberOfDocuments)
        {
            var u = Matrix<double>.Build.DenseOfArray(documentTopicMatrix);
            var w = Matrix<double>.Build.Dense(numberOfTopics, numberOfTopics);
            for (int k = 0; k < numberOfTopics; k++)
            {
                w[k, k] = lambdaMatrix[0, k];
            }
            var vt = Matrix<double>.Build.DenseOfArray(topicWordMatrix);
            var test = u.Multiply(w).Multiply(vt);
            var org = Matrix<double>.Build.DenseOfArray(documentWordMatrix);
            printResults2(numberOfDocuments, org, test);
        }

        private static void printResults2(int numberOfDocuments, Matrix<double> org, Matrix<double> after)
        {
            for (int i = 0; i < numberOfDocuments; i++)
            {
                for (int j = i + 1; j < numberOfDocuments; j++)
                {
                    var orgCos = cosSimilarity(org.Row(i), org.Row(j));
                    var plsaCos = cosSimilarity(after.Row(i), after.Row(j));
                    Console.WriteLine("{2} - {3} orginal: {0:0.00} plsa: {1:0.00}", orgCos, plsaCos, i, j);
                }
            }
        }

        private static double likelihood(double[,] documentWordMatrix, int numberOfTopics, double[,] documentTopicMatrix,
            double[,] lambdaMatrix, double[,] topicWordMatrix, int numberOfDocuments, int numberOfWords)
        {
            var ret = 0.0;
            var docWord = new double[numberOfDocuments, numberOfWords];
            for (int i_document_index = 0; i_document_index < numberOfDocuments; i_document_index++)
            {
                for (int j_word_index = 0; j_word_index < numberOfWords; j_word_index++)
                {
                    double sum = 0.0;
                    for (int k_topic_index = 0; k_topic_index < numberOfTopics; k_topic_index++)
                    {
                        sum +=lambdaMatrix[0, k_topic_index] * documentTopicMatrix[i_document_index, k_topic_index] * topicWordMatrix[k_topic_index, j_word_index];
                    }
                    docWord[i_document_index, j_word_index] = sum;
                }
            }
            for (int i_document_index = 0; i_document_index < numberOfDocuments; i_document_index++)
            {
                for (int j_word_index = 0; j_word_index < numberOfWords; j_word_index++)
                {
                    ret += documentWordMatrix[i_document_index, j_word_index] * Math.Log(docWord[i_document_index, j_word_index]);
                }
            }
            return ret;
        }

        private static double cosSimilarity(Vector<double> a, Vector<double> b)
        {
            double dotProduct = Enumerable.Range(0, a.Count()).Sum(i => a[i] * b[i]);

            double firstVectorLength = Math.Sqrt(a.Sum(weight => Math.Pow(weight, 2)));
            double secondVectorLength = Math.Sqrt(b.Sum(weight => Math.Pow(weight, 2)));

            double lengths = (firstVectorLength * secondVectorLength);

            return (Math.Abs(lengths - 0.0) < double.Epsilon) ? 0 : dotProduct / lengths;
        }

//        private static double cosSimilarity(Vector<double> a, Vector<double> b)
//        {
//            double sum = 0.0;
//            double moda = 0.0;
//            double modb = 0.0;
//            
//            for (int i = 0; i < a.Count; i++)
//            {
//                sum = sum + a[i]*b[i];
//                moda = moda + a[i] * a[i];
//                modb = modb + b[i] * b[i];
//            }
//            double v = Math.Sqrt(moda) + Math.Sqrt(modb);
//            return Math.Acos(sum/v);
//        }

        public static T[,] TransposeRowsAndColumns<T>(T[,] arr)
        {
            int rowCount = arr.GetLength(0);
            int columnCount = arr.GetLength(1);
            T[,] transposed = new T[columnCount, rowCount];
            if (rowCount == columnCount)
            {
                transposed = (T[,])arr.Clone();
                for (int i = 1; i < rowCount; i++)
                {
                    for (int j = 0; j < i; j++)
                    {
                        T temp = transposed[i, j];
                        transposed[i, j] = transposed[j, i];
                        transposed[j, i] = temp;
                    }
                }
            }
            else
            {
                for (int column = 0; column < columnCount; column++)
                {
                    for (int row = 0; row < rowCount; row++)
                    {
                        transposed[column, row] = arr[row, column];
                    }
                }
            }
            return transposed;
        }


    }
}
