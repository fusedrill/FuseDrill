using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;


namespace WebApiSample.Controllers
{

    // Command for coverage
    // dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=lcov /p:CoverletOutput=./lcov.info 
    public class ExampleClass
    {
        public object? NonsenseQuery(int?[]? a)
        {
            if (a == null)
                return null;
            if (a.Length > 0)
                if (a.Length > 3 && a[3] == 1234567890)
                    throw new Exception("bug");
            return new object();
        }
    }

    public class FakeClass
    {
        public int? MyNullProperty { get; set; }
        public int MyProperty { get; set; }
    }

    public class Pet
    {
        public int Id { get; set; }

        [Required]
        public required string Type { get; set; }

        public required string FullName { get; set; }

        [Required]
        public PetType PetType { get; set; }
    }

    public enum PetType
    {
        Dog = 0,
        Cat = 1
    }

    [ApiController]
    [Route("[controller]")]
    public class PetsController : ControllerBase
    {
        private readonly List<Pet> _petsInMemoryStore = [];

        public PetsController(List<Pet> petsInMemoryStore)
        {
            _petsInMemoryStore = petsInMemoryStore;
        }

        //[HttpGet("FuzzTestingMutliParameters/{id}/{nullableId}/{guid}/{nullableGuid}/{nullablePetType}/{petType}")]
        //public ActionResult<List<Pet>> FuzzTestingMutliParameters(int id, int? nullableId, Guid guid, Guid? nullableGuid, PetType? nullablePetType, PetType petType) => _petsInMemoryStore;

        //[HttpGet("NullableFuzz")]
        //public ActionResult<List<Pet>> FuzzTestingParameters([FromBody] FakeClass fakeClass) => _petsInMemoryStore;

        //[HttpGet("Fuzz")]
        //public ActionResult<List<Pet>> NullFuzzTestingParameters([FromBody] FakeClass? nullableFakeClass) => _petsInMemoryStore;

        [HttpGet]
        public ActionResult<List<Pet>> GetAll() => _petsInMemoryStore;

        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<Pet> GetById(int id)
        {
            var pet = _petsInMemoryStore.FirstOrDefault(p => p.Id == id);

            #region snippet_ProblemDetailsStatusCode
            if (pet == null)
            {
                return NotFound();
            }
            #endregion

            return pet;
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<Pet> Create(Pet pet)
        {
            pet.Id = _petsInMemoryStore.Any() ? _petsInMemoryStore.Max(p => p.Id) + 1 : 1;
            _petsInMemoryStore.Add(pet);

            return CreatedAtAction(nameof(GetById), new { id = pet.Id }, pet);
        }

        [HttpPut]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<Pet> Put(Pet pet)
        {
            var toModify = _petsInMemoryStore.FirstOrDefault(item => item.Id == pet.Id);
            if (toModify == null)
                return NotFound();

            toModify.PetType = pet.PetType;
            toModify.Type = pet.Type;
            toModify.FullName = pet.FullName;

            return Ok();
        }
    }
}