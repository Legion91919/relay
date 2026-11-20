# Product brief

> **Showcase project.** Kalahari Mobile (fictional), a regional mobile network bundling messaging with its SIMs is a fictional client and every name, user and number here is invented.
> Relay is an original private messaging app, built to show how the Baton model handles a consumer-scale product.

## Client
Kalahari Mobile (fictional), a regional mobile network bundling messaging with its SIMs

## Problem
The network's customers rely on foreign messaging apps that don't work on zero-rated data, so customers with no airtime can't message at all.

## Users
Prepaid customers (all day, low-end Android phones, patchy networks).

## Outcomes
Messages delivered in under 1 second when both phones are online; nothing lost when a phone drops off the network; works on 2G.

## Constraints
Messages stored encrypted at rest and deleted from the server once delivered to all recipients (or after 30 days); no message content in logs.

## Out of scope (first release)
Anything not needed for the outcomes above in the first three slices.

## Traits (for the architecture advisor)
- MillionsOfLiveConnections: "every active phone holds an open connection for instant delivery"
- PersonalData: "phone numbers, contacts, message content"
- SmallTeam: "four engineers for the pilot"

Global-scale profile used for the evolution plan: MillionsOfLiveConnections, GlobalScale, PersonalData.
