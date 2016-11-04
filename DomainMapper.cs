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
    }
}
