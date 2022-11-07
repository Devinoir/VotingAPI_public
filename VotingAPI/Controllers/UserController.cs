using ImageMagick;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using SqlKata.Compilers;
using SqlKata.Execution;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VotingAPI.Models;

namespace VotingAPI.Controllers
{
    [ApiController]
    [Route("api")]
    public class UserController : ControllerBase
    {
        private readonly ILogger<UserController> _logger;
        private string defaultExt = ".jpg";
        private MySqlConnection _connection;
        private QueryFactory _queryFactory;
        private MySqlCompiler _compiler = new MySqlCompiler();
        private JsonSerializer _serializer = new JsonSerializer();

        public UserController(ILogger<UserController> logger, MySqlConnection connection)
        {
            _logger = logger;
            _connection = connection;
            _queryFactory = new QueryFactory(_connection, _compiler);
        }

        #region GET

        /// <returns>All entry in the 'Users' table.</returns>
        [HttpGet]
        public JsonResult Get()
        {
            //_queryFactory.Query("Users").Get()
            try
            {
                return new JsonResult(_queryFactory.Query("Users").Get());
            }
            catch (Exception e)
            {
                return new JsonResult("Error while Get. " + e.Message) { StatusCode = 400 };
            }
        }

        /// <summary>
        /// Requires an AuthCode.
        /// </summary>
        /// <param name="authCode"></param>
        /// <returns>All users in the same event as the AuthCode. Id, Name, Kostuem, Bild and Stimme.</returns>
        [HttpGet("users/all/{authCode}")] //old result
        public JsonResult GetSameEventUsers(string authCode)
        {
            try
            {
                var eventId = _queryFactory.Query("Codes").Select("EventId").Where("AuthCode", authCode).FirstOrDefault<int?>();

                if (eventId == null)
                    throw new Exception("AuthCode does not exist in any current events.");

                var userList = _queryFactory
                    .Query("Codes")
                    .Join("Users", "Users.CodeId", "Codes.CodeId")
                    .Select("Users.Id", "Users.Name", "Users.Kostuem", "Users.Bild", "Users.Stimmen")
                    .Where("EventId", eventId)
                    .Get().ToList();

                var rng = new Random();

                var shuffledList = userList.OrderBy(a => rng.Next(100)).ToList();

                return new JsonResult(shuffledList);
            }
            catch (Exception e)
            {
                return new JsonResult("Error while getting same users in event. " + e.Message) { StatusCode = 400 };
            }
        }

        /// <summary>
        /// Requires an AuthCode.
        /// </summary>
        /// <param name="authCode"></param>
        /// <returns>All users in the same event as the AuthCode. Id, Name, Kostuem, Bild and Stimme.</returns>
        [HttpGet("user/top/{authCode}")]
        public JsonResult GetResults(string authCode)
        {
            try
            {
                var eventId = _queryFactory.Query("Codes").Select("EventId").Where("AuthCode", authCode).FirstOrDefault<int?>();
                int maxCount = 5;

                if (eventId == null)
                    throw new Exception("AuthCode does not exist in any current events.");

                if (!IsResultsOpen(eventId.Value) && !IsAdmin(authCode))
                    throw new Exception("Event results are closed.");

                var userList = _queryFactory
                    .Query("Codes")
                    .Join("Users", "Users.CodeId", "Codes.CodeId")
                    .Select("Users.Id", "Users.Name", "Users.Kostuem", "Users.Bild", "Users.Stimmen")
                    .Where("EventId", eventId)
                    .OrderByDesc("Users.Stimmen")
                    .Get().ToList();

                if (userList.Count < 5)
                    maxCount = userList.Count;

                var topList = userList.GetRange(0, maxCount);
                userList.RemoveRange(0, maxCount);



                return new JsonResult(new
                {
                    TopList = topList,
                    UserList = userList,
                });
            }
            catch (Exception e)
            {
                return new JsonResult("Error while getting same users in event. " + e.Message) { StatusCode = 400 };
            }
        }

        /// <summary>
        /// Generate random TestUsers
        /// </summary>
        /// <returns></returns>
        public JsonResult CreateTestUser()
        {
            try
            {
                for (int i = 0; i < 35; i++)
                {
                    Random rand = new Random();
                    var pictureId = Guid.NewGuid().ToString();
                    _queryFactory.Query("Users").AsInsert(new
                    {
                        Name = "Name" + rand.Next(500),
                        Kostuem = "Kostuem" + rand.Next(500),
                        Bild = $"{pictureId}.jpeg",
                        Stimmen = rand.Next(80),
                        CodeId = i + 16
                    }).Get();
                }

                return new JsonResult($"Added successfully!");
            }
            catch (Exception e)
            {
                return new JsonResult("Error while posting user. " + e.Message) { StatusCode = 400 };
            }
        }

        /// <summary>
        /// Gets the user information.
        /// </summary>
        /// <param name="authCode">The authentication code.</param>
        /// <returns></returns>
        [HttpGet("user/{authCode}")]
        public JsonResult GetUserInfo(string authCode)
        {
            return new JsonResult(new
            {
                IsValid = IsAuthCodeValid(authCode),
                IsRegistered = IsRegistered(authCode),
                HasVoted = HasVoted(authCode),
                IsAdmin = IsAdmin(authCode),
            });
        }

        /// <summary>
        /// Gets the event json.
        /// </summary>
        /// <param name="authCode">The authentication code.</param>
        /// <returns></returns>
        [HttpGet("event/{authCode}")]
        public JsonResult GetEventJson(string authCode)
        {
            try
            {
                var eventId = _queryFactory.Query("Codes").Select("EventId").Where("AuthCode", authCode).FirstOrDefault<int?>();

                if (eventId == null)
                    throw new Exception("AuthCode does not exist in any current events.");

                return new JsonResult(GetEvent(eventId.GetValueOrDefault()));
            }
            catch (Exception e)
            {
                return new JsonResult("Error while getting event." + e.Message);
            }
        }

        [HttpGet("resultOpen/{eventId}")]
        public bool IsResultsOpen(int eventId)
        {
            try
            {
                if (_queryFactory.Query("Events").Select("ResultsOpen").Where("EventId", eventId).FirstOrDefault<int>() == 1)
                    return true;

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }
        #endregion GET

        #region PUT

        /// <summary>
        /// Edit a User. Body needs Id. Colums that can be changed are: Name, Kostuem, Bild
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public JsonResult Put(User user)
        {
            try
            {
                _queryFactory.Query("Users").Where("Id", user.Id).AsUpdate(new
                {
                    Name = user.Name,
                    Kostuem = user.Kostuem,
                    Bild = user.Bild,
                }).Get();

                return new JsonResult("Updated successfully!");
            }
            catch (Exception e)
            {
                return new JsonResult("Error while updating user. " + e.Message) { StatusCode = 400 };
            }
        }

        /// <summary>
        /// Used to increment the votes of the given user.
        /// Body requires the UserId(s) of the recipiant(s), the numer of votes and the AuthCode that is used to vote.
        /// </summary>
        /// <param name="vRequest">Body requires the UserId of the recipiant and the AuthCode that is used to vote</param>
        /// <returns>response with EventId, UserId, AuthCode and the new vote count</returns>
        [Route("incr")]
        [HttpPut]
        public JsonResult IncrementVotesById(VoteRequest vRequest)
        {
            try
            {
                //Request Id is UserId

                #region Checks

                //Check Body
                if (vRequest == null)
                    throw new Exception("Body is missing or faulty.");

                //Check AuthCode not used
                if (HasVoted(vRequest.AuthCode))
                    throw new Exception("AuthCode was already used!");

                #endregion Checks

                //Increment the votes of the user
                var userIds = vRequest.Ids;
                var stimmenList = new List<int?>();
                var eventIdOfAuthCode = _queryFactory.Query("Codes").Select("EventId").Where("AuthCode", vRequest.AuthCode).First<int>();

                foreach (var userId in userIds)
                {
                    //Check if User and AuthCode are in the same Event
                    var eventIdOfUser = _queryFactory.Query("Codes").Join("Users", "Users.CodeId", "Codes.CodeId").Select("EventId").Where("Users.Id", userId).First<int>();
                    if (eventIdOfAuthCode != eventIdOfUser)
                        throw new Exception("User and AuthCode do not match EventId!");

                    //Check EventState
                    var evnt = GetEvent(eventIdOfUser);
                    if (GetEventState(evnt.EventId) != EventState.Voting)
                        throw new Exception("The event is not in voting phase!");

                    var newStimmen = GetVotesById(userId) + 1;

                    if (newStimmen == null)
                        throw new Exception("Couldn't get the current vote count.");

                    _queryFactory.Query("Users").Where("Id", userId).AsUpdate(new
                    {
                        Stimmen = newStimmen
                    }).Get();

                    stimmenList.Add(newStimmen);
                }

                //Set HasVoted status to 1
                _queryFactory.Query("Codes").Where("AuthCode", vRequest.AuthCode).AsUpdate(new
                {
                    HasVoted = 1
                }).Get();

                return new JsonResult(new
                {
                    UserIds = vRequest.Ids,
                    AuthCode = vRequest.AuthCode,
                    EventId = eventIdOfAuthCode,
                    Stimmen = stimmenList.ToArray(),
                });
            }
            catch (Exception e)
            {
                return new JsonResult($"Error while incrementing votes. " + e.Message) { StatusCode = 400 };
            }
        }

        /// <summary>
        /// Updates the time. Requires a Body with the new VoteOver and/or RegisterOver.
        /// </summary>
        /// <param name="evnt">The evnt.</param>
        /// <param name="authCode">The authentication code.</param>
        /// <returns></returns>
        [HttpPut("updateTime/{authCode}")]
        public JsonResult UpdateTime(Event evnt, string authCode)
        {
            try
            {
                if (!IsAdmin(authCode))
                    throw new Exception("Access denied.");

                var eventId = GetEventIdByAuthCode(authCode);

                if (evnt.RegisterOver != DateTime.MinValue && evnt.VoteOver != DateTime.MinValue)
                {
                    _queryFactory.Query("Events").Where("EventId", eventId).AsUpdate(new
                    {
                        RegisterOver = evnt.RegisterOver,
                        VoteOver = evnt.VoteOver,
                    }).Get();
                }

                if (evnt.RegisterOver != DateTime.MinValue)
                {
                    _queryFactory.Query("Events").Where("EventId", eventId).AsUpdate(new
                    {
                        RegisterOver = evnt.RegisterOver,
                    }).Get();
                }

                if (evnt.VoteOver != DateTime.MinValue)
                {
                    _queryFactory.Query("Events").Where("EventId", eventId).AsUpdate(new
                    {
                        VoteOver = evnt.VoteOver,
                    }).Get();
                }

                return new JsonResult($"Times of event '{eventId}' were succesfully updated");
            }
            catch (Exception e)
            {
                return new JsonResult("Error while updating time. " + e.Message);
            }
        }

        [HttpPut("resultsState/{state}/{authCode}")]
        public JsonResult ResultsStateAdmin(string state, string authCode)
        {
            try
            {
                if (!IsAdmin(authCode))
                    throw new Exception("Access denied.");

                int numState;
                string jsonMessage = "";

                switch (state.ToLower())
                {
                    case "open":
                        numState = 1;
                        jsonMessage = $"Successfully opened the results for event ";
                        break;
                    case "close":
                        numState = 0;
                        jsonMessage = $"Successfully closed the results for event ";
                        break;
                    default:
                        throw new Exception("Couldn't define state");
                }

                var eventId = GetEventIdByAuthCode(authCode);

                _queryFactory.Query("Events").Where("EventId", eventId).AsUpdate(new
                {
                    ResultsOpen = numState,
                }).Get();

                return new JsonResult(jsonMessage + eventId);
            }
            catch (Exception e)
            {
                return new JsonResult("Error while opening Results if result are open. " + e.Message);
            }
        }

        #endregion PUT

        #region POST

        /// <summary>
        /// Create a new User. Colums: Name, Kostuem
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        [HttpPost("register/{authCode}")]
        public JsonResult Post(User user, string authCode)
        {
            try
            {
                #region Checks

                //AuthCode check if exists
                if (!IsAuthCodeValid(authCode))
                    throw new Exception("AuthCode is invalid!");

                var codeId = _queryFactory
                    .Query("Codes")
                    .Select("CodeId")
                    .Where("AuthCode", authCode)
                    .First<string>();

                if (_queryFactory.Query("Users").Where("CodeId", codeId).Get().ToArray().Length != 0)
                    throw new Exception("AuthCode was already used!");

                if (string.IsNullOrEmpty(user.Name) || string.IsNullOrEmpty(user.Kostuem))
                    throw new Exception("Name or Kostüm is missing!");

                #endregion Checks

                //GetImageId
                string imageId = _queryFactory.Query("Images").Select("ImageId").Where("CodeId", codeId).FirstOrDefault<string>();

                _queryFactory.Query("Users").AsInsert(new
                {
                    Name = user.Name,
                    Kostuem = user.Kostuem,
                    CodeId = codeId,
                    Bild = imageId
                }).Get();

                return new JsonResult($"Added {user.Name} successfully!");
            }
            catch (Exception e)
            {
                return new JsonResult("Error while posting user. " + e.Message) { StatusCode = 400 };
            }
        }

        /// <summary>
        /// Used to save a picture to the filesystem
        /// </summary>
        /// <param name="authCode"></param>
        /// <returns>filepath</returns>
        [HttpPost("upload/{authCode}")]
        public JsonResult Upload(string authCode)
        {
            try
            {
                if (!IsAuthCodeValid(authCode))
                    throw new Exception("AuthCode is invalid.");

                var pictureGuid = Guid.NewGuid().ToString();

                var httpRequest = Request.Form;
                var postedFile = httpRequest.Files[0];
                var physicalPath = $"img/{pictureGuid}{defaultExt}";
                var newCrop = 0;

                using (var stream = new FileStream($"img/uncompressed/{pictureGuid}_uc{defaultExt}", FileMode.Create))
                {
                    postedFile.CopyTo(stream);
                }

                using (MagickImage image = new MagickImage(postedFile.OpenReadStream()))
                {
                    image.Format = image.Format;
                    image.Resize(800, 800);

                    if (image.Width >= image.Height)
                        newCrop = image.Height;
                    else
                        newCrop = image.Width;

                    image.Crop(newCrop, newCrop, Gravity.Center);
                    image.Resize(400, 400);
                    image.Quality = 80;
                    image.Write(physicalPath);
                }

                //Add picture name to user table
                //Images update
                var codeId = GetCodeIdFromAuthCode(authCode);

                //Check and update if entry already exist
                if (_queryFactory.Query("Images").Select("CodeId").Where("CodeId", codeId).FirstOrDefault<int?>() != null)
                {
                    _queryFactory.Query("Images").Where("CodeId", codeId).AsUpdate(new
                    {
                        ImageId = $"{pictureGuid}"
                    }).Get();
                }
                else
                {
                    _queryFactory.Query("Images").AsInsert(new
                    {
                        ImageId = $"{pictureGuid}",
                        CodeId = codeId,
                    }).Get();
                }

                return new JsonResult($"{pictureGuid}");
            }
            catch (Exception e)
            {
                return new JsonResult("Error while uploading image. " + e.Message)
                {
                    StatusCode = 400
                };
            }
        }

        #endregion POST

        #region DELETE

        /// <summary>
        /// Delete a User by UserId
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("deleteOnId/{userId}/{authcode}")]
        public JsonResult DeleteOnId(int userId, string authCode)
        {
            try
            {
                if (!IsAdmin(authCode))
                    throw new Exception("Access denied.");

                _queryFactory.Query("Users").Where("Id", userId).AsDelete().Get();

                return new JsonResult($"User with ID '{userId}' was successfully deleted.");
            }
            catch (Exception e)
            {
                return new JsonResult("Error while deleting user. " + e.Message) { StatusCode = 400 };
            }
        }

        /// <summary>
        /// Delete a User by AuthCode
        /// </summary>
        /// <returns></returns>
        [HttpDelete("deleteOnAuthOld/{userAuthCode}/{authcode}")]
        public JsonResult DeleteOnAuthOld(string userAuthCode, string authCode)
        {
            try
            {
                if (!IsAdmin(authCode))
                    throw new Exception("Access denied.");

                var user = GetUserByAuthCode(userAuthCode);
                if (user == null)
                    throw new Exception("User not found");

                _queryFactory.Query("Users").Where("Id", user.Id).AsDelete().Get();

                return new JsonResult($"User with ID '{user.Id}' was successfully deleted.");
            }
            catch (Exception e)
            {
                return new JsonResult("Error while deleting user. " + e.Message) { StatusCode = 400 };
            }
        }

        /// <summary>
        /// Delete a User by AuthCode
        /// </summary>
        /// <returns></returns>
        [HttpDelete("deleteOnAuth/{userAuthCode}/{authcode}")]
        public JsonResult DeleteOnAuth(string userAuthCode, string authCode)
        {
            try
            {
                if (!IsAdmin(authCode))
                    throw new Exception("Access denied.");

                var userId = GetUserIdByAuthCode(userAuthCode);

                if (userId == null)
                    throw new Exception("User not found");

                _queryFactory.Query("Users").Where("Id", userId).AsDelete().Get();

                return new JsonResult($"User with ID '{userId}' was successfully deleted.");
            }
            catch (Exception e)
            {
                return new JsonResult("Error while deleting user. " + e.Message) { StatusCode = 400 };
            }
        }

        #endregion DELETE

        #region HELPER
        private int? GetEventIdByAuthCode(string authCode)
        {
            try
            {
                return _queryFactory.Query("Codes").Select("EventId").Where("AuthCode", authCode).FirstOrDefault<int?>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Calculates the state of the event.
        /// </summary>
        /// <param name="eventId">The eventId.</param>
        /// <returns></returns>
        private EventState GetEventState(int eventId)
        {
            try
            {
                Event evnt = GetEvent(eventId);

                var time = DateTime.UtcNow.AddHours(1);

                if (time > evnt.RegisterOver && time < evnt.VoteOver)
                    return EventState.Voting;

                if (DateTime.Now > evnt.VoteOver)
                    return EventState.End;

                return EventState.Registration;
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// Determines whether the specified authentication code is registered.
        /// </summary>
        /// <param name="authCode">The authentication code.</param>
        /// <returns>
        ///   <c>true</c> if the specified authentication code is registered; otherwise, <c>false</c>.
        /// </returns>
        private bool? IsRegistered(string authCode)
        {
            try
            {
                if (_queryFactory.Query("Codes").Select("Users.CodeId").Join("Users", "Users.CodeId", "Codes.CodeId").Where("Codes.AuthCode", authCode).FirstOrDefault<int?>() == null)
                    return false;

                return true;
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// Determines whether the specified authentication code has voted.
        /// </summary>
        /// <param name="authCode">The authentication code.</param>
        /// <returns>
        ///   <c>true</c> if the specified authentication code has voted; otherwise, <c>false</c>.
        /// </returns>
        private bool HasVoted(string authCode)
        {
            try
            {
                if (_queryFactory.Query("Codes").Select("HasVoted").Where("AuthCode", authCode).First<bool>())
                    return true;

                return false;
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// Gets the codeId from  an authentication code.
        /// </summary>
        /// <param name="authCode">The authentication code.</param>
        /// <returns></returns>
        private int? GetCodeIdFromAuthCode(string authCode)
        {
            try
            {
                return _queryFactory.Query("Codes").Select("CodeId").Where("AuthCode", authCode).FirstOrDefault<int?>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Get User Model by UserId
        /// </summary>
        /// <param name="authCode"></param>
        /// <returns>User. Returns null on error.</returns>
        private User GetUserByAuthCode(string authCode)
        {
            try
            {
                var userId = _queryFactory.Query("Users").Join("Codes", "Users.CodeId", "Codes.CodeId").Select("Users.Id").Where("Codes.AuthCode", authCode).FirstOrDefault<int?>();

                if (userId == null)
                    return null;

                var x = JsonConvert.SerializeObject(_queryFactory.Query("Users").Where("Id", userId).First());

                User user = JsonConvert.DeserializeObject<User>(x);
                return user;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private int? GetUserIdByAuthCode(string authCode)
        {
            try
            {
                return _queryFactory.Query("Users").Join("Codes", "Users.CodeId", "Codes.CodeId").Select("Users.Id").Where("Codes.AuthCode", authCode).FirstOrDefault<int?>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Get votes from the given UserId
        /// </summary>
        /// <param name="id"></param>
        /// <returns>Numer of votes</returns>
        public int? GetVotesById(int id)
        {
            try
            {
                return _queryFactory.Query("Users").Select("Stimmen").Where("Id", id).FirstOrDefault<int>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Get Event from EventId
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private Event GetEvent(int id)
        {
            try
            {
                return _queryFactory.Query("Events").Where("EventId", id).FirstOrDefault<Event>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Check if AuthCode belongs to Admin
        /// </summary>
        /// <param name="authCode"></param>
        /// <returns></returns>
        private bool IsAdmin(string authCode)
        {
            try
            {
                return _queryFactory.Query("Codes").Select("IsAdmin").Where("AuthCode", authCode).FirstOrDefault<bool>();
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if AuthCode is valid
        /// </summary>
        /// <param name="authCode"></param>
        /// <returns></returns>
        private bool IsAuthCodeValid(string authCode)
        {
            try
            {
                if (_queryFactory.Query("Codes").Select("CodeId").Where("AuthCode", authCode).FirstOrDefault<int?>() == null)
                    return false;

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    #endregion HELPER
}