using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradX.Api.Errors;
using TradX.Domain.Common;

namespace TradX.UnitTests.Api;

/// <summary>Vérifie la traduction de chaque exception en réponse HTTP sûre.</summary>
public sealed class GlobalExceptionHandlerTests
{
    public static TheoryData<Exception, int, string> DomainCases => new()
    {
        { new NotFoundException("Annonce"), 404, "resource.not_found" },
        { new ForbiddenException(), 403, "access.forbidden" },
        { new ConflictException("x.dup", "Doublon"), 409, "x.dup" },
        { new BusinessRuleException("x.rule", "Règle"), 422, "x.rule" },
        { new AuthenticationFailedException(), 401, "auth.failed" },
    };

    [Theory]
    [MemberData(nameof(DomainCases))]
    public void Domain_exceptions_map_to_expected_status_and_code(Exception exception, int status, string code)
    {
        var problem = GlobalExceptionHandler.ToProblemDetails(exception);

        problem.Status.ShouldBe(status);
        problem.Extensions["code"].ShouldBe(code);
        problem.Detail.ShouldBe(exception.Message);
    }

    [Fact]
    public void Unexpected_exception_returns_500_without_leaking_internal_message()
    {
        var problem = GlobalExceptionHandler.ToProblemDetails(new InvalidOperationException("secret connection string"));

        problem.Status.ShouldBe(StatusCodes.Status500InternalServerError);
        problem.Detail!.ShouldNotContain("secret");
    }

    [Fact]
    public void Validation_exception_returns_400_with_camel_case_field_errors()
    {
        var exception = new ValidationException([
            new ValidationFailure("Email", "E-mail invalide."),
            new ValidationFailure("Address.City", "Ville obligatoire."),
        ]);

        var problem = GlobalExceptionHandler.ToProblemDetails(exception);

        var validation = problem.ShouldBeOfType<ValidationProblemDetails>();
        validation.Status.ShouldBe(400);
        validation.Errors["email"].ShouldBe(["E-mail invalide."]);
        validation.Errors["address.city"].ShouldBe(["Ville obligatoire."]);
    }

    [Fact]
    public void Concurrency_exception_returns_409()
    {
        var problem = GlobalExceptionHandler.ToProblemDetails(new DbUpdateConcurrencyException());

        problem.Status.ShouldBe(409);
        problem.Extensions["code"].ShouldBe("concurrency.conflict");
    }

    [Fact]
    public void Bad_http_request_keeps_its_status_code()
    {
        var problem = GlobalExceptionHandler.ToProblemDetails(new BadHttpRequestException("too big", 413));

        problem.Status.ShouldBe(413);
    }
}
