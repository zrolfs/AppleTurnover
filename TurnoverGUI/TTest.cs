﻿using FlashLFQ;
using Meta.Numerics.Statistics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace AppleTurnover
{
    public static class TTest
    {
        public static void CompareProteinsAcrossFiles(List<string> filenames, List<PeptideTurnoverObject> allProteins, Dictionary<string, PoolParameters> poolParameterDictionary)
        {
            if (filenames.Count == 0)
            {
                return;
            }
            string outputDirectory = Path.GetDirectoryName(filenames.First());
            for (int i = 0; i < filenames.Count; i++)
            {
                string fileOne = filenames[i];
                PoolParameters paramsOne = poolParameterDictionary[fileOne];
                //get the proteins for this file
                List<PeptideTurnoverObject> proteinsForFileOne = allProteins.Where(x => fileOne.Equals(x.FileName)).OrderBy(x => x.Protein).ToList();

                for (int j = i + 1; j < filenames.Count; j++)
                {
                    string fileTwo = filenames[j];
                    PoolParameters paramsTwo = poolParameterDictionary[fileTwo];
                    List<string> linesToWrite = new List<string>();
                    //add header
                    linesToWrite.Add("Protein\tFold Change\tNeg. log(p-Value)\tHalf-life " + fileOne + "\tHalf-life " + fileTwo);

                    List<PeptideTurnoverObject> proteinsForFileTwo = allProteins.Where(x => fileTwo.Equals(x.FileName)).OrderBy(x => x.Protein).ToList();

                    //get the overlap between them
                    int a = 0;
                    int b = 0;
                    while (a < proteinsForFileOne.Count && b < proteinsForFileTwo.Count)
                    {
                        PeptideTurnoverObject proteinOne = proteinsForFileOne[a];
                        PeptideTurnoverObject proteinTwo = proteinsForFileTwo[b];
                        int comparison = (proteinOne.Protein).CompareTo(proteinTwo.Protein);
                        if (comparison == 0)
                        {
                            //do the comparison (t-test of montecarlos, which dramatically overestimates the sample size)
                            //Sample sampleOne = new Sample(proteinOne.MonteCarloKbis.Select(x => Math.Log10(2) / x));
                            //Sample sampleTwo = new Sample(proteinTwo.MonteCarloKbis.Select(x => Math.Log10(2) / x));
                            //TestResult result = Sample.StudentTTest(sampleOne, sampleTwo);
                            //linesToWrite.Add(proteinOne.Protein + "\t" + (Math.Log2(sampleTwo.Median) - Math.Log2(sampleOne.Median)).ToString() + '\t' +
                            //    (-1*Math.Log10(result.Probability)).ToString() + '\t' + (Math.Log10(2) / proteinOne.Kbi).ToString() + '\t' + (Math.Log10(2) / proteinTwo.Kbi).ToString());

                            //do the comparison (t-test of normalized ratios for all timepoints)
                            double averageKbi = (proteinOne.Kbi + proteinTwo.Kbi) / 2;
                            double normalizedHalfLife = Math.Log(2) / (averageKbi); //this is the day we're going to normalize all of the relative fractions to

                            double[] expectedOriginalRatiosOne = NonLinearRegression.PredictRelativeFractionUsingThreeCompartmentModel(paramsOne.Kst, paramsOne.Kbt, paramsOne.Koa, proteinOne.Kbi, proteinOne.Timepoints);
                            double[] expectedUpdatedRatiosOne = NonLinearRegression.PredictRelativeFractionUsingThreeCompartmentModel(paramsOne.Kst, paramsOne.Kbt, paramsOne.Koa, proteinOne.Kbi, proteinOne.Timepoints);
                            double[] expectedOriginalRatiosTwo = NonLinearRegression.PredictRelativeFractionUsingThreeCompartmentModel(paramsTwo.Kst, paramsTwo.Kbt, paramsTwo.Koa, proteinTwo.Kbi, proteinTwo.Timepoints);
                            double[] expectedUpdatedRatiosTwo = NonLinearRegression.PredictRelativeFractionUsingThreeCompartmentModel(paramsTwo.Kst, paramsTwo.Kbt, paramsTwo.Koa, proteinTwo.Kbi, proteinTwo.Timepoints);
                            double[] normalizedRatiosOne = new double[expectedOriginalRatiosOne.Length];
                            double[] normalizedRatiosTwo = new double[expectedOriginalRatiosTwo.Length];
                            for (int index = 0; index < proteinOne.RelativeFractions.Length; index++)
                            {
                                normalizedRatiosOne[index] = proteinOne.RelativeFractions[index] - expectedOriginalRatiosOne[index] + expectedUpdatedRatiosOne[index];
                            }
                            for (int index = 0; index < proteinTwo.RelativeFractions.Length; index++)
                            {
                                normalizedRatiosTwo[index] = proteinTwo.RelativeFractions[index] - expectedOriginalRatiosTwo[index] + expectedUpdatedRatiosTwo[index];
                            }
                            Sample sampleOne = new Sample(normalizedRatiosOne);
                            Sample sampleTwo = new Sample(normalizedRatiosTwo);
                            TestResult result = Sample.StudentTTest(sampleOne, sampleTwo);
                            linesToWrite.Add(proteinOne.Protein + "\t" + (Math.Log2(Math.Log(2) / proteinTwo.Kbi) - Math.Log2(Math.Log(2) / proteinOne.Kbi)).ToString() + '\t' +
                                (-1 * Math.Log10(result.Probability)).ToString() + '\t' + (Math.Log10(2) / proteinOne.Kbi).ToString() + '\t' + (Math.Log10(2) / proteinTwo.Kbi).ToString());

                            a++;
                            b++;
                        }
                        else if (comparison < 0)
                        {
                            a++;
                        }
                        else
                        {
                            b++;
                        }
                    }
                    File.WriteAllLines(Path.Combine(outputDirectory, "Comparison_" + Path.GetFileNameWithoutExtension(fileOne) + "vs" + Path.GetFileNameWithoutExtension(fileTwo) + ".tsv"), linesToWrite);
                }
            }
        }

        public static void CompareProteoformsWithinFiles(List<string> filenames, List<PeptideTurnoverObject> allProteins, Dictionary<string, PoolParameters> poolParameterDictionary)
        {
            for (int fileIndex = 0; fileIndex < filenames.Count; fileIndex++)
            {
                string filename = filenames[fileIndex];
                PoolParameters poolParams = poolParameterDictionary[filename];
                List<PeptideTurnoverObject> proteinsForThisFile = allProteins.Where(x => filename.Equals(x.FileName)).OrderBy(x => x.Protein).ToList();
                List<string> linesToWrite = new List<string>();
                linesToWrite.Add("Protein A\tProtein B\tHalf-life A\tHalf-life B\tFold Change\tNeg. log(p-Value)");

                int indexOfNextProteoformFamily = 0;
                for (int i = 0; i < proteinsForThisFile.Count; i++)
                {
                    string currentProtein = proteinsForThisFile[i].Protein.Split('_')[0];
                    indexOfNextProteoformFamily++;
                    for (; indexOfNextProteoformFamily < proteinsForThisFile.Count; indexOfNextProteoformFamily++)
                    {
                        if (!currentProtein.Equals(proteinsForThisFile[indexOfNextProteoformFamily].Protein.Split('_')[0]))
                        {
                            break;
                        }
                    }

                    for (; i < indexOfNextProteoformFamily; i++)
                    {
                        PeptideTurnoverObject proteinOne = proteinsForThisFile[i];
                        Sample sampleOne = new Sample(proteinOne.MonteCarloKbis.Select(x => Math.Log(2) / x));
                        for (int j = i + 1; j < indexOfNextProteoformFamily; j++)
                        {
                            PeptideTurnoverObject proteinTwo = proteinsForThisFile[j];
                            Sample sampleTwo = new Sample(proteinTwo.MonteCarloKbis.Select(x => Math.Log(2) / x));
                            //do the t-test
                            TestResult result = Sample.StudentTTest(sampleOne, sampleTwo);
                            try //sometimes crashes if stdev is zero
                            {
                                linesToWrite.Add(proteinOne.Protein + "\t" + proteinTwo.Protein + '\t' + sampleOne.Median.ToString() + '\t' + sampleTwo.Median.ToString() + '\t' +
                                    (Math.Log2(sampleTwo.Median) - Math.Log2(sampleOne.Median)).ToString() + '\t' + (-1 * Math.Log(result.Probability)).ToString());
                            }
                            catch
                            {
                                linesToWrite.Add(proteinOne.Protein + "\t" + proteinTwo.Protein + '\t' + sampleOne.Median.ToString() + '\t' + sampleTwo.Median.ToString() + '\t' +
                                (Math.Log2(sampleTwo.Median) - Math.Log2(sampleOne.Median)).ToString() + '\t' + "NA");
                            }
                        }
                    }
                    i--;
                }

                File.WriteAllLines(Path.Combine(Path.GetDirectoryName(filename), "ProteoformAnalysis_" + Path.GetFileNameWithoutExtension(filename) + ".tsv"), linesToWrite);
            }
        }
    }
}