namespace Common
{ 
    public class DiffObjectComparer
    {
        private const string NullAccordancyViolationMessage = "Null accordance of property '{0}' is different in instances: left value = '{1}', right value = '{2}'";
        private const string CollectionLengthViolationMessage = "Property '{0}' has different lengths";
        private const string EqualityViolationMessage = "Property '{0}' is not equal in instances: left value = '{1}', right value = '{2}'";

        public bool Compare<T>(T left, T right, out List<ComparisonResult> comparisonResults)
        {
            comparisonResults = new List<ComparisonResult>();
            var comparisonResult = new ComparisonResult(left, right, "Root");

            return ProcessProperties(comparisonResult, comparisonResults);
        }

        private bool ProcessProperties(ComparisonResult propertiesData, List<ComparisonResult> comparisonResults)
        {
            // check if both properties are NULL
            if (propertiesData.LeftValue == null && propertiesData.RightValue == null)
            {
                return true;
            }

            // check if both properties are initialized
            if (!ObjectsAreNullAccordant(propertiesData))
            {
                comparisonResults.Add(propertiesData);

                return false;
            }

            var propertyType = propertiesData.LeftValue.GetType();

            // check if property is directly comparable i.e. is primitive, value type etc.
            if (IsDirectlyComparable(propertyType))
            {
                return ProcessPrimitives(propertiesData, comparisonResults);
            }

            // property is collection
            if (IsCollection(propertyType))
            {
                return ProcessCollections(propertiesData, comparisonResults);
            }

            // property is any ref. type
            return ProcessObjects(propertiesData, comparisonResults);
        }

        private bool ProcessObjects(ComparisonResult propertiesData, List<ComparisonResult> comparisonResults)
        {
            bool result = true;
            foreach (PropertyInfo currentProperty in GetObjectProperties(propertiesData.LeftValue))
            {
                var currentComparisonResult = new ComparisonResult(currentProperty.GetValue(propertiesData.LeftValue),
                                                                  currentProperty.GetValue(propertiesData.RightValue),
                                                                  string.Join(".", propertiesData.PropertyName, currentProperty.Name));

                if (!ProcessProperties(currentComparisonResult, comparisonResults))
                {
                    result = false;
                }
            }

            return result;
        }

        private bool ProcessCollections(ComparisonResult propertiesData, List<ComparisonResult> comparisonResults)
        {
            int idx = 0;
            bool result = true;
            IEnumerator leftValueEnumerator = ((IEnumerable)propertiesData.LeftValue).GetEnumerator();
            IEnumerator rightValueEnumerator = ((IEnumerable)propertiesData.RightValue).GetEnumerator();

            // iterate while both collections has items accordingly
            while (leftValueEnumerator.MoveNext() == rightValueEnumerator.MoveNext())
            {
                try
                {
                    // if we reach end of collection - return results
                    if (leftValueEnumerator.Current == null)
                    {
                        return result;
                    }
                }
                catch (InvalidOperationException)
                {
                    return result;
                }

                string propertyName = StringHelper.FormatInvariant("{0}[{1}]", propertiesData.PropertyName, idx);
                var currentComparisonResult = new ComparisonResult(leftValueEnumerator.Current, leftValueEnumerator.Current, propertyName);
                if (!ProcessProperties(currentComparisonResult, comparisonResults))
                {
                    result = false;
                }

                idx++;
            }

            // if we got here - raise validation issue, that collections have different length
            propertiesData.Message = CreateViolationMessage(CollectionLengthViolationMessage, propertiesData);
            comparisonResults.Add(propertiesData);

            return false;
        }

        private bool ProcessPrimitives(ComparisonResult propertiesData, List<ComparisonResult> comparisonResults)
        {
            if (PrimitivesAreEqual(propertiesData))
            {
                return true;
            }

            comparisonResults.Add(propertiesData);

            return false;
        }

        private bool ObjectsAreNullAccordant(ComparisonResult propertiesData)
        {
            if ((propertiesData.LeftValue == null) == (propertiesData.RightValue == null))
            {
                return true;
            }

            propertiesData.Message = StringHelper.FormatCurrentCulture(NullAccordancyViolationMessage,
                                                                       propertiesData.PropertyName,
                                                                       propertiesData.LeftValue ?? "NULL",
                                                                       propertiesData.RightValue ?? "NULL");

            return false;
        }

        private bool PrimitivesAreEqual(ComparisonResult propertiesData)
        {
            // all possible primitives comparison approaches
            var typedLeftValue = propertiesData.LeftValue as IComparable;
            if ((propertiesData.LeftValue == null && propertiesData.RightValue == null) ||
                (typedLeftValue != null && typedLeftValue.CompareTo(propertiesData.RightValue) == 0) ||
                Equals(propertiesData.LeftValue, propertiesData.RightValue))
            {
                return true;
            }

            propertiesData.Message = StringHelper.FormatCurrentCulture(EqualityViolationMessage,
                                                                         propertiesData.PropertyName,
                                                                         propertiesData.LeftValue,
                                                                         propertiesData.RightValue);

            return false;
        }

        /// <summary>
        /// Determines whether value instances of the specified <see cref="Type"/> can be directly compared.
        /// </summary>
        private bool IsCollection(Type type)
        {
            return typeof(IEnumerable).IsAssignableFrom(type);
        }

        /// <summary>
        /// Determines whether value instances of the specified <see cref="Type"/> can be directly compared.
        /// </summary>
        private bool IsDirectlyComparable(Type type)
        {
            return typeof(IComparable).IsAssignableFrom(type) || type.IsPrimitive || type.IsValueType;
        }

        /// <summary>
        /// Retrieves all public, non-static object properties which can be read from passed <see cref="objectToExtract"/>
        /// </summary>
        /// <returns>Comparable object properties</returns>
        private IEnumerable<PropertyInfo> GetObjectProperties(object objectToExtract)
        {
            if (objectToExtract == null)
            {
                return new List<PropertyInfo>();
            }

            var type = objectToExtract.GetType();

            return type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => p.CanRead);
        }

        private string CreateViolationMessage(string mask, ComparisonResult comparisonResult)
        {
            if (mask == CollectionLengthViolationMessage)
            {
                return StringHelper.FormatInvariant(mask, comparisonResult.PropertyName);
            }

            return StringHelper.FormatInvariant(mask, comparisonResult.PropertyName, comparisonResult.LeftValue, comparisonResult.RightValue);
        }
    }

    public class ComparisonResult
    {
        public object LeftValue { get; set; }

        public object RightValue { get; set; }

        public string PropertyName { get; set; }

        public string Message { get; set; }

        public ComparisonResult(object left, object right, string propertyName)
        {
            LeftValue = left;
            RightValue = right;
            PropertyName = propertyName;
        }
    }
}
