using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace EngineerMindset.C_.LINQ
{
    internal class Select_SelectMany
    {
        public void test()
        {
//.Select is like a map function. It goes through each element in a collection
//and applies a transformation to it, returning a new collection with the same number of elements.

//Example:

            List<int> numbers = new List<int> { 1, 2, 3 };
            var doubled = numbers.Select(n => n * 2);
            // Result: { 2, 4, 6 }


//.SelectMany does two things:

//It applies a transformation to each element, just like .Select.
//It then flattens the resulting collections into one single collection.
            List<string> words = new List<string> { "Hi", "Bye" };
            var characters = words.SelectMany(word => word.ToCharArray());
            // Result: { 'H', 'i', 'B', 'y', 'e' }



            //another example:
            List<List<int>> nestedInts = new List<List<int>>
{
    new List<int> { 1, 2, 3 },
    new List<int> { 4, 5, 6 },
    new List<int> { 7, 8, 9 }
};

            var transformedFlattenedInts = nestedInts
                .SelectMany(innerList => innerList.Select(n => n * 2));

            // transformedFlattenedInts will be: 2, 4, 6, 8, 10, 12, 14, 16, 18
        }
    }
}