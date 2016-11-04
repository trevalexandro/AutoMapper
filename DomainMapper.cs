using dal = RSI.Internal.TrainingTracker.Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using RSI.Internal.TrainingTracker.Domain.Models;
using System.Reflection;
using System.Linq.Expressions;
using System.Collections;

namespace RSI.Internal.TrainingTracker.Domain.Mappers
{
    public static class DomainMapper
    {
        /// <summary>
        /// Maps an object to another object with the same property names, this also handles nested objects that need mapping themselves
        /// </summary>
        /// <typeparam name="TInputObjectType">The type that you are mapping from, this object should already exist</typeparam>
        /// <typeparam name="TOutputObjectType">The type that you are mapping to, an object of this type will be returned</typeparam>
        /// <param name="inputObject">The object being mapped, the type must match TInputObjectType</param>
        /// <param name="specificMapping">This is for custom mapping, properties will be mapped according to name by default, however this allows for you to override
        /// that convention</param>
        /// <param name="outputObject">The object being mapped to, if this is left null a new instance of TOutputObjectType will be created</param>
        /// <returns>An object of type TOutputObjectType with properties mapped from the object passed in</returns>
        public static TOutputObjectType Map<TInputObjectType, TOutputObjectType>(TInputObjectType inputObject, Action<TInputObjectType, TOutputObjectType> specificMapping = null, TOutputObjectType outputObject = null) where TInputObjectType : class where TOutputObjectType : class
        {
            //If the object being passed in is null, return the default value for TOutputObjectType
            //this prevents a null reference exception downstream when mapping objects that are properties on another object
            if (inputObject == null)
            {
                return default(TOutputObjectType);
            }
            //Create instance of TOutputObjectType if it is null
            outputObject = outputObject ?? Activator.CreateInstance<TOutputObjectType>();
            //A property can either be a single object or collection of objects, if it is a collection call the IterateOverCollection method
            if (typeof(IEnumerable).IsAssignableFrom(typeof(TOutputObjectType)))
            {
                IterateOverCollection((IEnumerable)inputObject, (IEnumerable)outputObject);
            }
            //If the property is a single object, call the IterateOverProperties method
            else
            {
                IterateOverProperties(inputObject, outputObject);
            }
            //If a custom mapping of properties is specified, invoke the action before returning outputObject
            if (specificMapping != null)
            {
                specificMapping.Invoke(inputObject, outputObject);
            }
            return outputObject;
        }

        /// <summary>
        /// Accesses each property on an object being mapped and sets the value on the object being mapped to
        /// </summary>
        /// <typeparam name="TInputObjectType">The type of object being mapped</typeparam>
        /// <typeparam name="TOutputObjectType">The type of object that's being mapped to</typeparam>
        /// <param name="inputObject">The object being mapped</param>
        /// <param name="outputObject">The object being mapped to</param>
        private static void IterateOverProperties<TInputObjectType, TOutputObjectType>(TInputObjectType inputObject, TOutputObjectType outputObject)
        {
            //Get a collection of all object types in the System Assembly
            var systemAssemblyTypes = Assembly.GetExecutingAssembly().GetType().Module.Assembly.GetExportedTypes();
            //For each property on inputObject, try to find corresponding property on outputObject(names must be the same)
            foreach (var prop in inputObject.GetType().GetProperties())
            {
                var propToSet = outputObject.GetType().GetProperties().FirstOrDefault(p => p.Name == prop.Name);
                //If property is found, set it
                if (propToSet != null)
                {
                    //If property is a user defined type or collection of user defined types, 
                    //make a call back to the Map method and set the properties on the object(s)
                    if (!systemAssemblyTypes.Any(t => t == propToSet.PropertyType) && !propToSet.PropertyType.GenericTypeArguments.Any(gt => systemAssemblyTypes.Any(t => t == gt)))
                    {
                        propToSet.SetValue(outputObject, typeof(DomainMapper).GetMethod("Map", BindingFlags.Public | BindingFlags.Static)
                            .MakeGenericMethod(prop.PropertyType, propToSet.PropertyType).Invoke(null, new object[] { prop.GetValue(inputObject), null, null }));
                    }
                    //If property is not user defined(string, int, etc.), set it
                    else
                    {
                        propToSet.SetValue(outputObject, prop.GetValue(inputObject));
                    }
                }
            }
        }

        /// <summary>
        /// If a property is a collection of objects that need to be mapped, this method will map every object in the collection to make a collection of objects that
        /// represent a property on the object being mapped to
        /// </summary>
        /// <typeparam name="TInputObjectType">The type of collection being mapped</typeparam>
        /// <typeparam name="TOutputObjectType">The type of collection being mapped to</typeparam>
        /// <param name="inputCollection">The collection being mapped</param>
        /// <param name="outputCollection">The collection being mapped to, this will be empty when initially passed in</param>
        private static void IterateOverCollection<TInputObjectType, TOutputObjectType>(TInputObjectType inputCollection, TOutputObjectType outputCollection) where TInputObjectType : IEnumerable where TOutputObjectType : IEnumerable
        {
            //Get the type of objects in inputCollection
            var typeInInputCollection = inputCollection.GetType().GetGenericArguments().SingleOrDefault();
            //If the objects in the inputCollection aren't user defined, set the outputCollection to inputCollection as mapping isn't necessary
            if (Assembly.GetExecutingAssembly().GetType().Module.Assembly.GetExportedTypes().Any(t => t == typeInInputCollection))
            {
                outputCollection = (TOutputObjectType)(object)inputCollection;
            }
            //If the objects are user defined, map the properties on each object
            else
            {
                //Get the type of objects in outputCollection
                var typeInOutputCollection = outputCollection.GetType().GetGenericArguments().SingleOrDefault();
                foreach (var obj in inputCollection)
                {
                    ((IList)outputCollection).Add(typeof(DomainMapper).GetMethod("Map", BindingFlags.Public | BindingFlags.Static)
                        .MakeGenericMethod(typeInInputCollection, typeInOutputCollection).Invoke(null, new object[] { obj, null, null }));
                }
            }
        }

        ///// <summary>
        ///// Extension method that allows a mapped object to be mapped again, this allows an aggregate object to be mapped from multiple objects
        ///// </summary>
        ///// <typeparam name="TInputObjectType">The type that you are mapping from, this object should already exist</typeparam>
        ///// <typeparam name="TOutputObjectType">The type of the object that this extension method is invoked on and that 
        ///// you are mapping to, an object of this type will be returned</typeparam>
        ///// <param name="outputObject">The object that this method is invoked on, this same object will be returned once the
        ///// mapping is done</param>
        ///// <param name="inputObject">The object being mapped, the type must match TInputObjectType</param>
        ///// <param name="specificMapping">This is for custom mapping, properties will be mapped according to name by default, however this allows for you to override
        ///// that convention</param>
        ///// <returns>The object that invoked this method with properties mapped from the object passed in</returns>
        //public static TOutputObjectType MapAgain<TInputObjectType, TOutputObjectType>(this TOutputObjectType outputObject, TInputObjectType inputObject, Action<TInputObjectType, TOutputObjectType> specificMapping = null) where TInputObjectType : class where TOutputObjectType : class
        //{
        //    return Map(inputObject, specificMapping, outputObject);
        //}

        ///// <summary>
        ///// Extension method that maps an object and adds it to an existing collection of already mapped objects, this 
        ///// allows for multiple objects to be mapped from either one or many objects
        ///// </summary>
        ///// <typeparam name="TInputObjectType">The type that you are mapping from, this object should already exist</typeparam>
        ///// <typeparam name="TOutputObjectType">The type that you are mapping to, an object of this type will be added to 
        ///// the collection of mapped objects </typeparam>
        ///// <param name="currentCollection">The collection that calls this extension method, a new instance of this collection
        ///// will be returned with the newly mapped object added</param>
        ///// <param name="inputObject">The object being mapped, the type must match TInputObjectType</param>
        ///// <param name="specificMapping">This is for custom mapping, properties will be mapped according to name by default, however this allows for you to override
        ///// that convention</param>
        ///// <returns>A new instance of the collection that invoked this method with the mapped object added</returns>
        //public static IEnumerable<object> MapAnother<TInputObjectType, TOutputObjectType>(this IEnumerable<object> currentCollection, TInputObjectType inputObject, Action<TInputObjectType, TOutputObjectType> specificMapping = null) where TInputObjectType : class where TOutputObjectType : class
        //{
        //    return new List<object>(currentCollection.ToList()) { Map(inputObject, specificMapping) };
        //}

        ///// <summary>
        ///// Extension method that maps an object and returns a new collection containing the mapped object and the object the extension method was called on,  
        ///// this allows for multiple objects to be mapped from either one or many objects using method chaining
        ///// </summary>
        ///// <typeparam name="TInputObjectType">The type that you are mapping from, this object should already exist</typeparam>
        ///// <typeparam name="TOutputObjectType">The type that you are mapping to, an object of this type will be added to 
        ///// the new collection</typeparam>
        ///// <param name="firstObject">The object that calls this extension method, it will be added to the new collection with the mapped object</param>
        ///// <param name="inputObject">The object being mapped, the type must match TInputObjectType</param>
        ///// <param name="specificMapping">This is for custom mapping, properties will be mapped according to name by default, however this allows for you to override
        ///// that convention</param>
        ///// <returns>A new collection containing the object that invoked this extension method and a newly mapped object of TOutputObjectType</returns>
        //public static IEnumerable<object> MapAnother<TInputObjectType, TOutputObjectType>(this object firstObject, TInputObjectType inputObject, Action<TInputObjectType, TOutputObjectType> specificMapping = null) where TInputObjectType : class where TOutputObjectType : class
        //{
        //    return new List<object> { firstObject, Map(inputObject, specificMapping) };
        //}

        //public static Colleague MapColleague(dal.User User)
        //{
        //    return new Colleague()
        //    {
        //        FirstName = User.FirstName,
        //        LastName = User.LastName,
        //        MiddleName = User.MiddleName,
        //        Id = User.Id,
        //        //Manager = 
        //        //NextReviewDate = 
        //        StartDate = User.StartDate,
        //        TerminationDate = User.TerminationDate
        //        //TrainingManager = 
        //    };

        //}

        //public static dal.User MapColleague(Colleague User)
        //{
        //    return new dal.User()
        //    {
        //        Active = User.Active,
        //        EmailAddress = User.EmailAddress,
        //        FirstName = User.FirstName,
        //        MiddleName = User.MiddleName,
        //        LastName = User.LastName,
        //        Location = User.Location,
        //        Manager = MapColleague(User.Manager),
        //        OAUserId = User.OAUserId,
        //        PracticeArea = User.PracticeArea,
        //        StartDate = User.StartDate,
        //        TerminationDate = User.TerminationDate,
        //        TrainingManager = MapColleague(User.TrainingManager),
        //        Id = User.Id
        //    };
        //}

        //public static Interest MapInterest(dal.TrainingInterest interest)
        //{
        //    return new Interest
        //    {
        //        Id = interest.Id,
        //        Description = interest.Description,
        //        Name = interest.Name,
        //        TrainingPlan = MapTrainingPlan(interest.TrainingPlan)
        //    };
        //}

        //public static dal.TrainingInterest MapInterest(Interest interest)
        //{
        //    return new dal.TrainingInterest
        //    {
        //        Id = interest.Id,
        //        Description = interest.Description,
        //        Name = interest.Name,
        //        TrainingPlan = MapTrainingPlan(interest.TrainingPlan)
        //    };
        //}

        //public static ProjectStatus MapProjectStatus(dal.ProjectStatus projectStatus)
        //{
        //    return new ProjectStatus
        //    {
        //        Description = projectStatus.Description,
        //        Id = projectStatus.Id,
        //        Name = projectStatus.Name
        //    };
        //}

        //public static dal.ProjectStatus MapProjectStatus(ProjectStatus projectStatus)
        //{
        //    return new dal.ProjectStatus
        //    {
        //        Description = projectStatus.Description,
        //        Id = projectStatus.Id,
        //        Name = projectStatus.Name
        //    };
        //}

        //public static Project MapProject(dal.Project project)
        //{
        //    return new Project
        //    {
        //        Id = project.Id,
        //        Name = project.Name,
        //        Description = project.Description,
        //        OAProjectId = project.OAProjectId,
        //        Status = MapProjectStatus(project.Status)
        //    };
        //}

        //public static dal.Project MapProject(Project project)
        //{
        //    return new dal.Project
        //    {
        //        Id = project.Id,
        //        Name = project.Name,
        //        Description = project.Description,
        //        OAProjectId = project.OAProjectId,
        //        Status = MapProjectStatus(project.Status)
        //    };
        //}

        //public static TraineeProject MapTraineeProject(dal.TraineeProject traineeProject)
        //{
        //    return new TraineeProject
        //    {
        //        Id = traineeProject.Id,
        //        Colleague = MapColleague(traineeProject.User),
        //        TrainingPlan = MapTrainingPlan(traineeProject.TrainingPlan),
        //        Project = MapProject(traineeProject.Project)
        //    };
        //}

        //public static dal.TraineeProject MapTraineeProject(TraineeProject traineeProject)
        //{
        //    return new dal.TraineeProject
        //    {
        //        Id = traineeProject.Id,
        //        User = MapColleague(traineeProject.Colleague),
        //        TrainingPlan = MapTrainingPlan(traineeProject.TrainingPlan),
        //        Project = MapProject(traineeProject.Project)
        //    };
        //}

        //public static GoalType MapGoalType(dal.GoalType goalType)
        //{
        //    return new GoalType
        //    {
        //        Description = goalType.Description,
        //        Id = goalType.Id,
        //        Name = goalType.Name
        //    };
        //}

        //public static dal.GoalType MapGoalType(GoalType goalType)
        //{
        //    return new dal.GoalType
        //    {
        //        Description = goalType.Description,
        //        Id = goalType.Id,
        //        Name = goalType.Name
        //    };
        //}

        //public static GoalStatus MapGoalStatus(dal.GoalStatus goalStatus)
        //{
        //    return new GoalStatus
        //    {
        //        Id = goalStatus.Id,
        //        Name = goalStatus.Name,
        //        Description = goalStatus.Description
        //    };
        //}

        //public static dal.GoalStatus MapGoalStatus(GoalStatus goalStatus)
        //{
        //    return new dal.GoalStatus
        //    {

        //    }
        //}

        //public static LogItem MapLogItem(dal.LogItem logItem)
        //{
        //    return new LogItem
        //    {
        //        Id = logItem.Id,
        //        LogDate = logItem.LogDate,
        //        Goal = MapGoal(logItem.Goal),
        //        GoalStatus = logItem.GoalStatus
        //    };
        //}

        //public static Goal MapGoal(dal.Goal goal)
        //{
        //    return new Goal
        //    {
        //        Id = goal.Id,
        //        Description = goal.Description,
        //        ExpectedCompDate = goal.ExpectedCompDate,
        //        Name = goal.Name,
        //        Colleague = MapColleague(goal.Colleague),
        //        TrainingPlan = MapTrainingPlan(goal.TrainingPlan),
        //        GoalType = MapGoalType(goal.GoalType),
        //        Log = goal.Log
        //    };
        //}

        //public static TrainingPlan MapTrainingPlan(dal.TrainingPlan trainingPlan)
        //{
        //    return new TrainingPlan
        //    {
        //        LastReviewDate = trainingPlan.LastReviewDate,
        //        PlanDate = trainingPlan.DateCreated,
        //        Interests = trainingPlan.Interests.Select(i => MapInterest(i)),
        //        Trainee = MapColleague(trainingPlan.Trainee),
        //        TraineeProjects = trainingPlan.TraineeProjects.Select(tp => MapTraineeProject(tp)),
        //        Goals = trainingPlan.Goals
        //    };
        //}

        //public static dal.TrainingPlan MapTrainingPlan(TrainingPlan trainingPlan)
        //{
        //    return new dal.TrainingPlan
        //    {

        //    };
        //}

        //public static dal.Goal MapGoal(Goal Goal)
        //{
        //    return new dal.Goal()
        //    {
        //        //Description = Goal.Description,
        //        //ExpectedCompDate = Goal.ExpectedCompDate,
        //        //GoalId = Goal.Go
        //    };
        //}
    }
}