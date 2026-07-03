using System.Collections.Generic;
using NickeltownPOSV4.Models.Membership;

namespace NickeltownPOSV4.Data.Sqlite;

/// <summary>Exact wording extracted from Individual Membership Form 2026 - 2027.docx.</summary>
internal static class MembershipFormContentSeed
{
    internal static IReadOnlyList<(string Key, string? Title, string Body, int SortOrder)> Sections { get; } =
    [
        (
            MembershipFormContentKeys.ClubHeader,
            null,
            """
            Nickeltown Flounderers Inc Auto Club
            ABN: 45 087 371 412
            PO Box 31, Kambalda WA 6442
            Ph: 0410 065 002
            Email: nickeltown@gmail.com
            """,
            10),
        (
            MembershipFormContentKeys.FormTitle,
            null,
            "INDIVIDUAL MEMBERSHIP APPLICATION 2026/2027",
            20),
        (
            MembershipFormContentKeys.SectionVehicleDetails,
            "VEHICLE DETAILS",
            "VEHICLE DETAILS",
            30),
        (
            MembershipFormContentKeys.SectionApplicantDetails,
            "APPLICANTS DETAILS:",
            "APPLICANTS DETAILS:",
            40),
        (MembershipFormContentKeys.FieldSurname, null, "Surname", 50),
        (MembershipFormContentKeys.FieldGivenNames, null, "Given Names", 60),
        (
            MembershipFormContentKeys.FieldRegisteredVehicleNote,
            null,
            "*As per your registered vehicle with the Department of Transport, no nicknames to be used for club registration purposes",
            70),
        (MembershipFormContentKeys.FieldChildrenUnder18, null, "Children (Under the age of 18 years)", 80),
        (MembershipFormContentKeys.FieldAddress, null, "Address", 90),
        (MembershipFormContentKeys.FieldPostCode, null, "Post Code", 100),
        (MembershipFormContentKeys.FieldDateOfBirth, null, "Date of Birth", 110),
        (MembershipFormContentKeys.FieldEmailAddress, null, "Email Address", 120),
        (MembershipFormContentKeys.FieldPhoneNumber, null, "Phone Number", 130),
        (MembershipFormContentKeys.FieldMobile, null, "Mobile", 140),
        (MembershipFormContentKeys.FieldMakeModel, null, "Make/Model", 150),
        (MembershipFormContentKeys.FieldYear, null, "Year", 160),
        (MembershipFormContentKeys.FieldBodyType, null, "Body Type", 170),
        (MembershipFormContentKeys.FieldEngine, null, "Engine", 180),
        (MembershipFormContentKeys.FieldRegistrationNumber, null, "Registration Number", 190),
        (MembershipFormContentKeys.FieldClubRego, null, "Club Rego", 200),
        (MembershipFormContentKeys.FieldColour, null, "Colour", 210),
        (MembershipFormContentKeys.FieldModifications, null, "Modifications (If Any)", 220),
        (
            MembershipFormContentKeys.Declaration,
            null,
            """
            I hereby apply for membership of the Nickeltown Flounderers Inc Auto Club Kambalda (The Club), which will commence on payment of the application fee and acceptance by the Club. Once accepted membership shall be for the remainder of the membership year, which will cease on the 30th June in each year. I agree to be bound by any rules made from time to time and/or varied by the Club and that this membership is bound by the conditions printed below, which I have read as evidenced by my signature affixed hereunder.
            """,
            230),
        (MembershipFormContentKeys.FeeStructureIntro, null, "Structure of Fees:", 240),
        (
            MembershipFormContentKeys.FeeStructureJulyDecember,
            null,
            "Join between July 1st and December 31st",
            250),
        (
            MembershipFormContentKeys.FeeStructureJanuaryJune,
            null,
            "Join between January 1st and June 30th",
            260),
        (
            MembershipFormContentKeys.LiabilityWaiver,
            null,
            """
            The Club is exempt from all or any liability arising from:
            Loss of damage to any vehicle or property
            The personal injury to myself or any passengers
            As the case may be, howsoever caused, whether by negligence or otherwise which may occur whilst Engaged in any Club activity
            """,
            270),
        (MembershipFormContentKeys.FieldSignature, null, "Signature", 280),
        (MembershipFormContentKeys.FieldDate, null, "Date", 290),
        (
            MembershipFormContentKeys.SectionClubUseOnly,
            "CLUB USE ONLY",
            "CLUB USE ONLY",
            300),
        (
            MembershipFormContentKeys.ClubUseReceiptIssued,
            null,
            "Receipt Issued Date // Date Membership Accepted:",
            310),
        (
            MembershipFormContentKeys.ClubUseAddedToRegister,
            null,
            "Added To Register/Email & Text Distribution List",
            320),
        (
            MembershipFormContentKeys.ClubUseCardIssued,
            null,
            "Membership Card Issued Welcome Bag Issued",
            330),
        (
            MembershipFormContentKeys.FieldAdditionalVehiclesNote,
            null,
            "For Additional Vehicles, please attach further information",
            340),
        (
            MembershipFormContentKeys.FieldAdditionalComments,
            null,
            "Additional Comments (what do you want from your club)",
            350),
        (
            MembershipFormContentKeys.InformationForApplicants,
            "INFORMATION FOR APPLICANTS",
            """
            If your application is accepted, your name and address, as provided on the previous page, must be recorded in a Register of Members and be made available to other members, upon request, under section 53 of the Associations Incorporation Act.
            If the obligations under the Associations Incorporation Act are not complied with the Association can be wound Up.
            You can contact the Association via email to nickeltown@gmail.com , via phone on 0410 065 002

            You can access or correct personal information (your name & address) by contacting the Association as indicated above.
            """,
            360),
        (
            MembershipFormContentKeys.OtherInformation,
            "OTHER INFORMATION",
            """
            If your application is accepted, you are entitled to inspect and make a copy of the register of members under Section 53 of the Associations Incorporation Act.

            If you application is accepted, you are entitled to inspect and make a copy of the rules (constitution) of the association under section 35 of the Associations Incorporation Act. These rules are readily available via email, as well as hard copy.
            If your application for membership is rejected by the Committee: you may give notice of your intention to appeal within 14 days of being advised of the rejection (rule 5(4)). The Association in a general meeting, no later than the next annual general meeting, must confirm or set aside the decision of the Committee rejecting your application, after giving you a reasonable opportunity to be heard or to make written representations to the general meeting (rule 5(5)).
            """,
            370),
    ];
}
