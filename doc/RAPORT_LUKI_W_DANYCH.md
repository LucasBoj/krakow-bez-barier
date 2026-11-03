# Luki w danych i bariery: liczby z raportu

Generuje: `dotnet run --project src/Tools -- report krakow` (z plików katalogu w repozytorium). Te same liczby pokazuje strona `/miasto`; na stronie dochodzą bariery potwierdzone przez urząd z bazy hosta.

Miejsca to cele wizyt: wszystkie kategorie katalogu poza ławkami, przystankami i kopertami. „Bez danych” nie liczy hałasu otoczenia z mapy, bo mapa obejmuje każde miejsce.

| Grupa | Miejsca | Z wejściem dla wózków | Bez danych o dostępności | Z danymi o toalecie | Z hałasem otoczenia | Z hałasem w miejscu |
|---|---|---|---|---|---|---|
| **Razem** | 12589 | 2194 (17,4%) | 10388 (82,5%) | 205 (1,6%) | 12588 (100,0%) | 10 |
| Urzędy | 175 | 26 (14,9%) | 149 (85,1%) | 0 (0,0%) | 175 (100,0%) | 1 |
| Kultura | 929 | 143 (15,4%) | 785 (84,5%) | 5 (0,5%) | 929 (100,0%) | 8 |
| Zdrowie | 1041 | 191 (18,3%) | 850 (81,7%) | 4 (0,4%) | 1040 (99,9%) | 1 |
| Edukacja | 448 | 17 (3,8%) | 431 (96,2%) | 1 (0,2%) | 448 (100,0%) | 0 |
| Gastronomia | 2459 | 431 (17,5%) | 2024 (82,3%) | 41 (1,7%) | 2459 (100,0%) | 0 |
| Pozostałe | 7537 | 1386 (18,4%) | 6149 (81,6%) | 154 (2,0%) | 7537 (100,0%) | 0 |

## Dzielnice (poza granicami dzielnic: 8 miejsc)

| Dzielnica | Miejsca | Z wejściem dla wózków | Bez danych o dostępności | Z danymi o toalecie | Z hałasem otoczenia | Z hałasem w miejscu |
|---|---|---|---|---|---|---|
| I Stare Miasto | 3485 | 485 (13,9%) | 2998 (86,0%) | 46 (1,3%) | 3485 (100,0%) | 5 |
| II Grzegórzki | 733 | 169 (23,1%) | 563 (76,8%) | 15 (2,0%) | 733 (100,0%) | 2 |
| III Prądnik Czerwony | 438 | 99 (22,6%) | 338 (77,2%) | 9 (2,1%) | 438 (100,0%) | 0 |
| IV Prądnik Biały | 854 | 123 (14,4%) | 731 (85,6%) | 4 (0,5%) | 854 (100,0%) | 0 |
| V Krowodrza | 764 | 268 (35,1%) | 495 (64,8%) | 17 (2,2%) | 764 (100,0%) | 1 |
| VI Bronowice | 355 | 103 (29,0%) | 252 (71,0%) | 7 (2,0%) | 355 (100,0%) | 0 |
| VII Zwierzyniec | 437 | 71 (16,2%) | 366 (83,8%) | 12 (2,7%) | 437 (100,0%) | 0 |
| VIII Dębniki | 807 | 171 (21,2%) | 635 (78,7%) | 22 (2,7%) | 807 (100,0%) | 1 |
| IX Łagiewniki-Borek Fałęcki | 264 | 27 (10,2%) | 237 (89,8%) | 5 (1,9%) | 264 (100,0%) | 0 |
| X Swoszowice | 205 | 22 (10,7%) | 183 (89,3%) | 1 (0,5%) | 205 (100,0%) | 0 |
| XI Podgórze Duchackie | 534 | 21 (3,9%) | 513 (96,1%) | 2 (0,4%) | 534 (100,0%) | 0 |
| XII Bieżanów-Prokocim | 571 | 73 (12,8%) | 498 (87,2%) | 7 (1,2%) | 571 (100,0%) | 0 |
| XIII Podgórze | 1060 | 157 (14,8%) | 903 (85,2%) | 23 (2,2%) | 1060 (100,0%) | 1 |
| XIV Czyżyny | 754 | 59 (7,8%) | 695 (92,2%) | 13 (1,7%) | 754 (100,0%) | 0 |
| XV Mistrzejowice | 352 | 20 (5,7%) | 332 (94,3%) | 1 (0,3%) | 352 (100,0%) | 0 |
| XVI Bieńczyce | 337 | 66 (19,6%) | 271 (80,4%) | 8 (2,4%) | 337 (100,0%) | 0 |
| XVII Wzgórza Krzesławickie | 108 | 4 (3,7%) | 104 (96,3%) | 1 (0,9%) | 108 (100,0%) | 0 |
| XVIII Nowa Huta | 523 | 252 (48,2%) | 270 (51,6%) | 12 (2,3%) | 523 (100,0%) | 0 |

## 20 miejsc do sprawdzenia w pierwszej kolejności (z 906 kandydatów, rozkład z dnia 07.10.2026)

Kandydaci: urzędy, biblioteki, muzea, instytucje kultury, szkoły oraz szpitale, przychodnie, poradnie, SP ZOZ, pogotowie i hospicja (rozpoznawane po nazwie; prywatne gabinety pomijamy), bez żadnej informacji o wejściu dla wózków. Natężenie ruchu przybliżamy liczbą odjazdów komunikacji miejskiej w dzień powszedni z przystanków w promieniu 300 m (rozkład GTFS ZTP); nie mamy danych o liczbie odwiedzających. Wynik = odjazdy × waga kategorii: urzędy i placówki zdrowia 2 (sprawy, których nie da się odłożyć), pozostałe 1. Przy równym wyniku decyduje nazwa.

| # | Miejsce | Kategoria | Dzielnica | Odjazdy w 300 m | Wynik | Mieszkańcy w 300 m |
|---|---|---|---|---|---|---|
| 1 | Izba Administracji Skarbowej w Krakowie, Aleja Zygmunta Krasińskiego 11b | Office | Stare Miasto | 5486 | 10972 | 2909 |
| 2 | Specjalistyczna Poradnia Diagnozy i Rehabilitacji Dzieci i Młodzieży z Wadą Słuchu PZG | Clinic | Stare Miasto | 4082 | 8164 | 2836 |
| 3 | Starostwo Powiatowe w Krakowie | Office | Krowodrza | 4082 | 8164 | 2792 |
| 4 | ДП "Документ", Pawia 5 | Office | Stare Miasto | 3878 | 7756 | 628 |
| 5 | Komenda Regionalna Straży Ochrony Kolei w Krakowie, Lubicz 14 | Office | Stare Miasto | 3565 | 7130 | 848 |
| 6 | Komisja Nadzoru Finansowego | Office | Stare Miasto | 3552 | 7104 | 816 |
| 7 | Agencja Mienia Wojskowego | Office | Stare Miasto | 3523 | 7046 | 302 |
| 8 | Szpital Uniwersytecki CM UJ | Clinic | Grzegórzki | 3026 | 6052 | 619 |
| 9 | Wody Polskie, Marszałka Józefa Piłsudskiego 22 | Office | Stare Miasto | 3013 | 6026 | 2342 |
| 10 | Sąd Apelacyjny w Krakowie | Office | Grzegórzki | 2979 | 5958 | 1463 |
| 11 | Sąd Rejonowy, Mogilska 17 | Office | Grzegórzki | 2979 | 5958 | 2062 |
| 12 | Najwyższa Izba Kontroli | Office | Krowodrza | 2963 | 5926 | 2824 |
| 13 | Powiatowy Inspektorat Nadzoru Budowlanego w Krakowie, Aleja Juliusza Słowackiego 20 | Office | Krowodrza | 2963 | 5926 | 3115 |
| 14 | Wojewódzki Inspektorat Nadzoru Budowlanego | Office | Krowodrza | 2963 | 5926 | 2824 |
| 15 | Centrum Powiadamiania Ratunkowego, Szlak 73a | Office | Stare Miasto | 2789 | 5578 | 677 |
| 16 | Komisariat Policji IV w Krakowie, Królewska 4 | Office | Krowodrza | 2740 | 5480 | 3727 |
| 17 | Rada i Zarząd Dzielnicy V Krowodrza | Office | Krowodrza | 2740 | 5480 | 2911 |
| 18 | Punkt Obsługi Programu WFOŚiGW "Czyste Powietrze" | Office | Czyżyny | 2723 | 5446 | 1624 |
| 19 | Szpital na Siemiradzkiego - Ośrodek Medycyny Matczyno-Płodowej | Clinic | Stare Miasto | 2707 | 5414 | 2659 |
| 20 | Narodowe Centrum Nauki, Twardowskiego 16 | Office | Dębniki | 2664 | 5328 | 1832 |

## Udogodnienia w zasięgu mieszkańców (300 m)

Mieszkańcy to stałe zameldowania w heksagonach 100 m z ewidencji ludności (MSIP); osoby zameldowane czasowo i niezameldowane nie są ujęte. Udogodnienie jest „w zasięgu”, gdy leży do 300 m w linii prostej od środka heksagonu. Seniorzy to osoby w wieku poprodukcyjnym (kobiety 60+, mężczyźni 65+) z ewidencji dzielnic. Ławki pochodzą z OpenStreetMap, więc braki w mapowaniu zaniżają ich zasięg.

Siatka zameldowań: stan na 30.06.2024, 701483 osób. Ewidencja dzielnic: stan na 31.12.2025, 703707 osób, w tym 181213 seniorów (25,8%) i 41356 osób 80+.

| Udogodnienie | Mieszkańcy w zasięgu | Bez udogodnienia | Szacunek: seniorzy bez udogodnienia | Korelacja z udziałem seniorów (r) |
|---|---|---|---|---|
| przystanek komunikacji miejskiej | 614390 (87,6%) | 87093 | 21301 | 0,69 |
| przystanek z wiatą | 572857 (81,7%) | 128626 | 31348 | 0,68 |
| ławka (także na przystanku) | 661656 (94,3%) | 39827 | 9245 | 0,48 |
| toaleta publiczna | 184382 (26,3%) | 517101 | 131111 | 0,53 |
| toaleta publiczna dostosowana | 96667 (13,8%) | 604816 | 155041 | 0,36 |
| miejsce postojowe dla osób z niepełnosprawnościami | 575098 (82,0%) | 126385 | 30009 | 0,48 |

| Dzielnica | Mieszkańcy (siatka) | Seniorzy | 80+ | przystanek komunikacji miejskiej | przystanek z wiatą | ławka (także na przystanku) | toaleta publiczna | toaleta publiczna dostosowana | miejsce postojowe dla osób z niepełnosprawnościami |
|---|---|---|---|---|---|---|---|---|---|
| I Stare Miasto | 26840 | 31,8% | 6,6% | 96,4% | 90,5% | 100,0% | 82,2% | 42,0% | 100,0% |
| II Grzegórzki | 29378 | 27,4% | 7,4% | 84,8% | 80,8% | 100,0% | 28,4% | 20,1% | 100,0% |
| III Prądnik Czerwony | 44651 | 28,9% | 7,5% | 92,4% | 92,2% | 99,6% | 27,3% | 20,6% | 97,9% |
| IV Prądnik Biały | 72418 | 23,1% | 5,5% | 93,5% | 86,4% | 95,2% | 18,7% | 7,8% | 79,9% |
| V Krowodrza | 30208 | 32,3% | 9,0% | 92,4% | 92,4% | 99,2% | 49,3% | 20,7% | 99,5% |
| VI Bronowice | 23903 | 26,5% | 7,7% | 83,4% | 69,4% | 93,7% | 42,3% | 31,1% | 85,0% |
| VII Zwierzyniec | 20084 | 27,9% | 6,3% | 90,0% | 80,5% | 88,6% | 17,1% | 11,1% | 46,3% |
| VIII Dębniki | 64570 | 20,4% | 4,3% | 79,8% | 77,4% | 86,5% | 24,3% | 11,3% | 67,9% |
| IX Łagiewniki-Borek Fałęcki | 15243 | 24,4% | 5,3% | 79,7% | 55,9% | 89,9% | 16,1% | 12,1% | 77,2% |
| X Swoszowice | 28907 | 20,2% | 3,7% | 73,5% | 63,5% | 73,4% | 4,7% | 3,6% | 30,7% |
| XI Podgórze Duchackie | 54403 | 26,4% | 4,0% | 89,0% | 87,1% | 98,6% | 21,1% | 0,0% | 81,7% |
| XII Bieżanów-Prokocim | 62034 | 26,8% | 5,7% | 85,4% | 79,0% | 96,9% | 14,2% | 11,0% | 84,4% |
| XIII Podgórze | 41038 | 17,6% | 2,9% | 88,3% | 72,7% | 93,2% | 28,9% | 20,4% | 83,1% |
| XIV Czyżyny | 34513 | 20,4% | 3,3% | 76,1% | 71,9% | 99,1% | 22,5% | 13,7% | 89,5% |
| XV Mistrzejowice | 50597 | 28,9% | 7,3% | 87,4% | 80,0% | 99,5% | 14,9% | 1,3% | 92,7% |
| XVI Bieńczyce | 36812 | 31,1% | 10,4% | 94,0% | 93,6% | 100,0% | 40,3% | 14,2% | 99,7% |
| XVII Wzgórza Krzesławickie | 19765 | 25,7% | 5,9% | 90,2% | 76,3% | 80,6% | 0,5% | 0,3% | 51,1% |
| XVIII Nowa Huta | 45541 | 30,3% | 6,1% | 94,4% | 92,6% | 91,0% | 39,4% | 27,8% | 82,2% |
