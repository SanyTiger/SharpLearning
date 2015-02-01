﻿using SharpLearning.Common.Interfaces;
using SharpLearning.Containers;
using SharpLearning.Containers.Matrices;
using SharpLearning.CrossValidation.TrainingValidationSplitters;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpLearning.CrossValidation.BiasVarianceAnalysis
{
    /// <summary>
    /// Bias variance analysis calculator for constructing learning curves.
    /// Learning curves can be used to determine if a model has high bias or high variance.
    /// 
    /// Solutions for model with high bias:
    ///  - Add more features.
    ///  - Use a more sophisticated model
    ///  - Decrease regularization.
    /// Solutions for model with high variance
    ///  - Use fewer features.
    ///  - Use more training samples.
    ///  - Increase Regularization.
    /// </summary>
    public class BiasVarianceLearningCurvesCalculator<TPrediction>
    {
        readonly ITrainingValidationIndexSplitter<double> m_trainingValidationIndexSplitter;
        readonly double[] m_samplePercentages;
        readonly IMetric<double, TPrediction> m_metric;

        /// <summary>
        /// Bias variance analysis calculator for constructing learning curves.
        /// Learning curves can be used to determine if a model has high bias or high variance.
        /// </summary>
        /// <param name="trainingValidationIndexSplitter"></param>
        /// <param name="metric">The error metric used</param>
        /// <param name="samplePercentages">A list of sample percentages determining the 
        /// training data used in each point of the learning curve</param>
        public BiasVarianceLearningCurvesCalculator(ITrainingValidationIndexSplitter<double> trainingValidationIndexSplitter, 
            IMetric<double, TPrediction> metric, double[] samplePercentages)
        {
            if (trainingValidationIndexSplitter == null) { throw new ArgumentException("trainingValidationIndexSplitter"); }
            if (samplePercentages == null) { throw new ArgumentNullException("samplePercentages"); }
            if (samplePercentages.Length < 1) { throw new ArgumentException("SamplePercentages length must be at least 1"); }
            if (metric == null) { throw new ArgumentNullException("metric");}

            m_trainingValidationIndexSplitter = trainingValidationIndexSplitter;
            m_samplePercentages = samplePercentages;
            m_metric = metric;
        }

        /// <summary>
        /// Returns a list of BiasVarianceLearningCurvePoints for constructing learning curves.
        /// The points contain sample size, training score and validation score. 
        /// </summary>
        /// <param name="learnerFactory"></param>
        /// <param name="observations"></param>
        /// <param name="targets"></param>
        /// <returns></returns>
        public List<BiasVarianceLearningCurvePoint> Calculate(IIndexedLearner<TPrediction> learnerFactory,
            F64Matrix observations, double[] targets)
        {
            var trainingValidationIndices = m_trainingValidationIndexSplitter.Split(targets);
            
            return Calculate(learnerFactory, observations, targets,
                trainingValidationIndices.TrainingIndices,
                trainingValidationIndices.ValidationIndices);
        }

        /// <summary>
        /// Returns a list of BiasVarianceLearningCurvePoints for constructing learning curves.
        /// The points contain sample size, training score and validation score. 
        /// </summary>
        /// <param name="learnerFactory"></param>
        /// <param name="observations"></param>
        /// <param name="targets"></param>
        /// <param name="trainingIndices">Indices that should be used for training</param>
        /// <param name="validationIndices">Indices that should be used for validation</param>
        /// <returns></returns>
        public List<BiasVarianceLearningCurvePoint> Calculate(IIndexedLearner<TPrediction> learnerFactory,
            F64Matrix observations, double[] targets, int[] trainingIndices, int[] validationIndices)
        {
            var validationTargets = targets.GetIndices(validationIndices);
            var learningCurves = new List<BiasVarianceLearningCurvePoint>();

            foreach (var samplePercentage in m_samplePercentages)
            {
                if (samplePercentage <= 0.0 || samplePercentage > 1.0)
                { throw new ArgumentException("Sample percentage must be larger than 0.0 and smaller than or equal to 1.0"); }

                var sampleSize = (int)(samplePercentage * trainingIndices.Length);
                sampleSize = sampleSize > 0 ? sampleSize : 1;

                var sampleIndices = trainingIndices.Take(sampleSize).ToArray();

                var model = learnerFactory.Learn(observations, targets, sampleIndices);
                var trainingPredictions = new TPrediction[sampleSize];

                for (int i = 0; i < trainingPredictions.Length; i++)
                {
                    trainingPredictions[i] = model.Predict(observations.GetRow(sampleIndices[i]));
                }

                var validationPredictions = new TPrediction[validationIndices.Length];
                for (int i = 0; i < validationIndices.Length; i++)
                {
                    validationPredictions[i] = model.Predict(observations.GetRow(validationIndices[i]));
                }

                var trainingTargets = targets.GetIndices(sampleIndices);
                learningCurves.Add(new BiasVarianceLearningCurvePoint(sampleSize,
                    m_metric.Error(trainingTargets, trainingPredictions),
                    m_metric.Error(validationTargets, validationPredictions)));
            }

            return learningCurves;
        }
    }
}