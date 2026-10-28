using FluentValidation;
using FSH.Modules.Ordering.Contracts.v1.Stores;
using FSH.Modules.Ordering.Domain;

namespace FSH.Modules.Ordering.Features.v1.Stores.StoreContacts;

public sealed class GetStoreContactsQueryValidator : AbstractValidator<GetStoreContactsQuery>
{
    public GetStoreContactsQueryValidator()
    {
        RuleFor(query => query.StoreId).NotEmpty();
    }
}

public sealed class ReplaceStoreContactsCommandValidator : AbstractValidator<ReplaceStoreContactsCommand>
{
    public ReplaceStoreContactsCommandValidator()
    {
        RuleFor(command => command.StoreId).NotEmpty();
        RuleFor(command => command.Contacts).NotNull()
            .Must(contacts => contacts.Count <= StoreContact.MaxPerStore)
            .WithMessage($"A store can have at most {StoreContact.MaxPerStore} contacts.")
            .Must(contacts => contacts
                .Select(contact => PhoneNumbers.Digits(contact.Phone))
                .Distinct(StringComparer.Ordinal)
                .Count() == contacts.Count)
            .WithMessage("Each contact phone can only be listed once per store.");
        RuleForEach(command => command.Contacts).ChildRules(contact =>
        {
            contact.RuleFor(item => item.Name).NotEmpty().MaximumLength(100);
            contact.RuleFor(item => item.Phone).NotEmpty().MaximumLength(40)
                .Must(phone => PhoneNumbers.Digits(phone).Length is >= StoreContact.MinPhoneDigits and <= 32)
                .WithMessage($"A contact phone needs {StoreContact.MinPhoneDigits} to 32 digits.");
        });
    }
}
