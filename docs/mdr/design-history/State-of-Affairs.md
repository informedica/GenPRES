# The State of the Affairs of the GenPres project.

The current (i.e. 2nd of December 2021) of the GenPres project will be described in this "Wiki blog".

## Introduction

The GenPres project aims to build a 'one-click' medical prescription system. All the necessary basic activities to create a medical prescription, like:

- Looking things up. For example the right dose.
- Calculations. Like the amount and frequency or the preparation of a prescription and
- Evaluation. Was the prescription applied to the right patient, when, how and to what effect 

will be covered by the GenPres system. This system in its prototypical form has already been used for years on a Pediatric Critical Care unit with a substantial amount of success.

The main challenge in this project is to enable all the necessary calculations in such way that the order of entry of the required quantitative data doesn't matter and the system always feeds back what the range is of the possible remaining quantitative variables. This enables the system to be able to calculate all possible scenarios from which the prescriber just needs to make a choice. Hence the one click.

The calculations involved and how to solve those are described [here](https://github.com/informedica/Informedica.GenPres.Lib/wiki/Informedica.GenOrder.Lib).

## Short History

This project actually started years ago. The 10th of June 2013 there was the first commit in a repository that was trying to solve this problem. Due to entry of some sensitive data the repository remains private. But a small presentation of the capabilities of the system was recorded and can be viewed at [Vimeo](https://vimeo.com/manage/videos/78822206). 

Currently we are working on a more definitive solution. 

## The Current State

The GenPres project has to tackle a number of problems:

1. [x] The calculations involved have to be solved in any possible order. This means that if you calculate something like, total dose = frequency x dose. Then either a total dose and a frequency or total dose and a dose or a dose and a frequency can be entered and the rest will be calculated. This has to be done for a whole set of equations in which variables can be reoccur. 
2. [x] All calculations have to performed without the need to explicitly supply the units of measurement. This is done by calculating the dependent units of measurement by the supplied values. All calculations are then performed in 'base units' and then converted back to the appropriate units.
3. [x] Calculations have to be performed using absolute precision quantities. Meaning 1/3 cannot be rounded to 0.333 as it is then impossible to know whether the resulting value is precise enough and whether this value has been calculated for other equations. Therefore, the GenPres system uses BigRationals. 
4. [x] The calculations are further complicated by the fact that this is actually a process of applying restrictions of the possible ranges of values. So in the case of calculating total dose = frequency x dose, each variable total dose, frequency and dose can be restricted by a minimum, maximum, set of increments or a specific set of values. Therefore, the calculation can look like total dose [60 .. 80] = frequency [2, 3, 4] x dose [120 .. 320]. 
4. [ ] The system has to be performant in doing the calculations. However, for some calculations the list of possible values can be large resulting in long calculation times. This has to be resolved using a mathematical modelling.
5. [ ] The system needs 2 large sources of information, 1) product information, which products are available and 2) dosing and preparation rules. Using these information sources the system is able to calculate the exact possible scenarios from which a clinician or health care worker can choose from.
6. [ ] In the end the users need to interact with the system. In order for the system to be usable on different devices a web based solution seems appropriate.

## The Big Issues

Overall there are 3 remaining issues that have to be resolved.

1. The performance problem. For this there a cooperation has started with the mathematical department of the Utrecht University. The aim is to model ranges such that they enable efficient calculations. 
2. Available information depends on a nation wide product database. There is already code to extract, transform and load all product data such that it can be used for calculations. For the dosing and preparation rules 
3. The user needs to interact with the system. A first usability project could be started with calculations limited to the scenarios in which the system is already performant. This is the case for all calculations involving an intermittent dosing regime or standard concentrations. 

## Specific Use Case

A first specific use case could be the creation of an emergency medication list, to be used in acute situations in pediatric medicine. Currently incentive is collected to enable funding of this project.

