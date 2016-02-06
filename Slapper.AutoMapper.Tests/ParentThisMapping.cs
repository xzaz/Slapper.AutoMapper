using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using NUnit.Framework;

namespace Slapper.Tests
{
    [TestFixture]
    public class ParentThisMapping : TestBase
    {
        public class Person
        {
            public string Name { get; set; }
            public string Firstname { get; set; }
            public Department Department { get; set; }
        }

        public class Department
        {
            public string Name { get; set; }
        }
        
        [Test]
        public void Can_Populate_Self_Object_Properties()
        {
            dynamic person = new ExpandoObject();
            person.PERSON_NAME = "Clark";
            person.DEPARTMENT_NAME = "IT";
            person.FIRSTNAME = "Ken";

            var list = new List<dynamic> { person };

            var pPerson = Slapper.AutoMapper.MapDynamic<Person>(list).FirstOrDefault();

            Assert.NotNull(pPerson);
            Assert.NotNull(pPerson.Name);
            Assert.NotNull(pPerson.Firstname);
            Assert.NotNull(pPerson.Department.Name);
        }
    }


}
