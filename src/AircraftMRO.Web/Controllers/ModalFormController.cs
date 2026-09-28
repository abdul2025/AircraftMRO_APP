using Microsoft.AspNetCore.Mvc;

namespace AircraftMRO.Web.Controllers;

/// <summary>
/// Create, edit, and delete forms render as modal content when requested with the modal header
/// (see <c>wwwroot/js/modal-forms.js</c>) and as full pages otherwise.
/// </summary>
public abstract class ModalFormController : Controller
{
    public const string ModalRequestHeader = "X-Modal-Request";
    public const string ModalReturnUrlHeader = "X-Modal-Return-Url";
    protected const string StatusMessageKey = "StatusMessage";
    protected const string ErrorMessageKey = "ErrorMessage";

    protected bool IsModalRequest => Request.Headers[ModalRequestHeader] == "true";

    /// <summary>The page the modal was opened from, accepted only when it is local to this site.</summary>
    protected string? ModalReturnUrl
    {
        get
        {
            if (!IsModalRequest)
            {
                return null;
            }

            var returnUrl = Request.Headers[ModalReturnUrlHeader].ToString();
            return Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        }
    }

    /// <summary>Renders the form as modal content (partial) or as a full page.</summary>
    protected IActionResult FormView(string action, object model, int statusCode = StatusCodes.Status200OK)
    {
        Response.StatusCode = statusCode;
        return IsModalRequest ? PartialView($"_{action}Form", model) : View(action, model);
    }

    /// <summary>After a successful save: a redirect for full pages, or its URL as JSON for the modal script.</summary>
    protected IActionResult Saved(string message, string redirectUrl)
    {
        TempData[StatusMessageKey] = message;
        return IsModalRequest ? Json(new { redirectUrl }) : Redirect(redirectUrl);
    }

    /// <summary>Decodes the base64 row version a form carried; false when it is missing or malformed.</summary>
    protected static bool TryDecodeRowVersion(string? value, out byte[] rowVersion)
    {
        rowVersion = [];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var buffer = new byte[value.Length];
        if (!Convert.TryFromBase64String(value, buffer, out var written) || written == 0)
        {
            return false;
        }

        rowVersion = buffer[..written];
        return true;
    }
}
